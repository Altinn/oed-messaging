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
using Microsoft.Extensions.Options;
using Polly;
using System.Net;
using System.Net.Sockets;

namespace Altinn.Dd.Correspondence.Extensions;

public static class ServiceCollectionExtensions
{
    private const string CorrespondenceScope = "altinn:serviceowner altinn:correspondence.write";
    private const int RetryCount = 3;

    public static IServiceCollection AddDdCorrespondenceService(
        this IServiceCollection services,
        string configSectionPath,
        Action<DdCorrespondenceOptions>? configureOptions = null)
    {
        services.AddOptionsWithValidateOnStart<DdCorrespondenceOptions>()
                .BindConfiguration(configSectionPath);

        return AddDdCorrespondenceServiceInternal(services, configureOptions);
    }

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

        services.AddTransient<IHandler<DdCorrespondenceDetails, CorrespondenceResult>, Features.Send.Handler>();
        services.AddTransient<IHandler<Query, Features.Search.Result>, Features.Search.Handler>();
        services.AddTransient<IHandler<Request, Features.Get.Result>, Features.Get.Handler>();
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

        maskinportenHttpClient!.AddHttpMessageHandler(() => new AsyncPolicyDelegatingHandler(CreateRetryPolicy()))
            .ConfigureHttpClient(httpClient =>
            {
                httpClient.BaseAddress = correspondenceOptions.Environment switch
                {
                    ApiEnvironment.Development => ApiEndpoints.PlatformTest, // TODO: Change when dev endpoint for platform is available
                    ApiEnvironment.Staging => ApiEndpoints.PlatformTest,
                    ApiEnvironment.Production => ApiEndpoints.PlatformProduction,
                    _ => throw new ArgumentOutOfRangeException($"Unknown environment: {correspondenceOptions.Environment}")
                };
            });
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<SocketException>()
            .OrResult(response =>
                response.StatusCode == HttpStatusCode.RequestTimeout ||
                response.StatusCode == HttpStatusCode.TooManyRequests ||
                (int)response.StatusCode >= 500)
            .WaitAndRetryAsync(
                RetryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    private sealed class AsyncPolicyDelegatingHandler : DelegatingHandler
    {
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;

        public AsyncPolicyDelegatingHandler(IAsyncPolicy<HttpResponseMessage> policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return _policy.ExecuteAsync(
                (ct) => base.SendAsync(request, ct),
                cancellationToken);
        }
    }
}