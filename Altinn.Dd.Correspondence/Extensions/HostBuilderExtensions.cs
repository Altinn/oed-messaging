using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Dd.Correspondence.Constants;
using Altinn.Dd.Correspondence.Features;
using Altinn.Dd.Correspondence.Features.Get;
using Altinn.Dd.Correspondence.Features.Search;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Options;
using Altinn.Dd.Correspondence.Options.Validators;
using Altinn.Dd.Correspondence.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Altinn.Dd.Correspondence.Extensions;

/// <summary>
/// Registration helpers that wire the correspondence client, its Maskinporten authentication and
/// its resilience pipeline into a service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string CorrespondenceScope = "altinn:serviceowner altinn:correspondence.write altinn:correspondence.read";
    private const int RetryCount = 3;

    /// <summary>
    /// Registers <see cref="Services.IDdCorrespondenceService"/>, binding its options from the
    /// given configuration section.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configSectionPath">Configuration section holding the correspondence options,
    /// for example <c>"DdConfig"</c>.</param>
    /// <param name="configureOptions">Optional callback to adjust the bound options.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddDdCorrespondenceService(
        this IServiceCollection services,
        string configSectionPath,
        Action<DdCorrespondenceOptions>? configureOptions = null)
    {
        services.AddOptionsWithValidateOnStart<DdCorrespondenceOptions>()
                .BindConfiguration(configSectionPath);

        return AddDdCorrespondenceServiceInternal(services, configureOptions);
    }

    /// <summary>
    /// Registers <see cref="Services.IDdCorrespondenceService"/>, configuring its options in code
    /// rather than binding them from configuration.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureOptions">Callback that supplies the correspondence options.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddDdCorrespondenceService(
        this IServiceCollection services,
        Action<DdCorrespondenceOptions>? configureOptions = null)
    {
        services.AddOptionsWithValidateOnStart<DdCorrespondenceOptions>();

        return AddDdCorrespondenceServiceInternal(services, configureOptions);
    }

    private static IServiceCollection AddDdCorrespondenceServiceInternal(
        IServiceCollection services,
        Action<DdCorrespondenceOptions>? configureOptions)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DdCorrespondenceOptions>, ValidateDdCorrespondenceOptions>());

        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        var correspondenceOptions = services.BuildServiceProvider()
            .GetRequiredService<IOptions<DdCorrespondenceOptions>>()
            .Value;
        correspondenceOptions.MaskinportenSettings.Scope = CorrespondenceScope;
        correspondenceOptions.MaskinportenSettings.ExhangeToAltinnToken = true;

        services.AddTransient<IHandler<DdCorrespondenceDetails, Result<ReceiptExternal>>, Features.Send.Handler>();
        services.AddTransient<IHandler<Query, Result<IEnumerable<Guid>>>, Features.Search.Handler>();
        services.AddTransient<IHandler<Request, Result<CorrespondenceOverview>>, Features.Get.Handler>();
        services.AddTransient<IDdCorrespondenceService, DdCorrespondenceService>();

        ConfigureMaskinportenHttpClient(services, correspondenceOptions);

        return services;
    }

    private static void ConfigureMaskinportenHttpClient(
        IServiceCollection services,
        DdCorrespondenceOptions correspondenceOptions)
    {
        var maskinportenSettings = correspondenceOptions.MaskinportenSettings;
        var maskinportenHttpClient = maskinportenSettings switch
        {
            { EncodedJwk: not null } => services.AddMaskinportenHttpClient<SettingsJwkClientDefinition, AltinnCorrespondenceClient>(maskinportenSettings),
            { EncodedX509: not null } => services.AddMaskinportenHttpClient<SettingsX509ClientDefinition, AltinnCorrespondenceClient>(maskinportenSettings),
            _ => throw new InvalidOperationException("MaskinportenSettings must specify either EncodedJwk or EncodedX509.")
        };

        maskinportenHttpClient!
            .ConfigureHttpClient(httpClient =>
            {
                httpClient.BaseAddress = correspondenceOptions.Environment switch
                {
                    ApiEnvironment.Development => ApiEndpoints.PlatformTest, // Altinn exposes no separate dev endpoint, so Development shares the test platform
                    ApiEnvironment.Staging => ApiEndpoints.PlatformTest,
                    ApiEnvironment.Production => ApiEndpoints.PlatformProduction,
                    _ => throw new ArgumentOutOfRangeException($"Unknown environment: {correspondenceOptions.Environment}")
                };
            })
            .AddStandardResilienceHandler(options =>
            {
                // Retry 408, 429 and 5xx, plus transport failures, with exponential backoff.
                // Jitter spreads retries so parallel callers do not resynchronise on a bad minute.
                options.Retry.MaxRetryAttempts = RetryCount;
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.UseJitter = true;

                // The standard pipeline adds timeouts the previous hand-rolled policy did not
                // have, so these are sized not to fail calls that used to succeed. Before 3.0.0
                // there was no per-attempt timeout at all and the ceiling was HttpClient's 100s
                // default; the total below keeps that ceiling.
                //
                // A slow-but-healthy Altinn is the case to protect: 10s was too tight for a large
                // correspondence body. Note that 4 attempts at 30s exceed the total, so against a
                // persistently slow endpoint the total timeout ends the call before the retry
                // budget is spent - which is the intended ordering.
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(100);

                // Sampling must span at least twice the attempt timeout.
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
            });
    }
}
