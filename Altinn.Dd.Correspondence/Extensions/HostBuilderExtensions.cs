using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Interfaces;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Dd.Correspondence.Constants;
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


    extension(IServiceCollection services)
    {
        public IServiceCollection AddDdCorrespondenceService(
            string configSectionPath,
            Action<DdCorrespondenceOptions>? configureOptions = null)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DdCorrespondenceOptions>, ValidateDdCorrespondenceOptions>());
            services.AddOptionsWithValidateOnStart<DdCorrespondenceOptions>()
                    .BindConfiguration(configSectionPath);
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }

            var correspondenceOptions = services.BuildServiceProvider().GetRequiredService<IOptions<DdCorrespondenceOptions>>().Value;
            correspondenceOptions.MaskinportenSettings.Scope = CorrespondenceScope;

            services.AddTransient<IDdCorrespondenceService, DdCorrespondenceService>();
            services.AddMaskinportenHttpClient<SettingsJwkClientDefinition, IDdCorrespondenceService, DdCorrespondenceService>(correspondenceOptions.MaskinportenSettings)
                .AddHttpMessageHandler(() => new AsyncPolicyDelegatingHandler(CreateRetryPolicy()))
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
            return services;
        }
    }

    ///// <summary>
    ///// Adds DD Messaging Service with Maskinporten authentication to the service collection.
    ///// This method follows the Altinn 3 pattern for registering HttpClients with Maskinporten.
    ///// </summary>
    ///// <typeparam name="TClientDefinition">The Maskinporten client definition type (e.g., SettingsJwkClientDefinition)</typeparam>
    ///// <param name="services">The service collection</param>
    ///// <param name="maskinportenSettings">Configuration section containing Maskinporten settings (must include ClientId)</param>
    ///// <param name="correspondenceSettings">Configuration section containing correspondence settings</param>
    ///// <returns>The service collection for chaining</returns>
    //public static IServiceCollection AddDdMessagingService<TClientDefinition>(
    //    this IServiceCollection services,
    //    IConfigurationSection maskinportenSettings,
    //    IConfigurationSection correspondenceSettings)
    //    where TClientDefinition : class, IClientDefinition
    //{
    //    // Bind and register correspondence settings
    //    var settings = correspondenceSettings.Get<CorrespondenceSettings>();
    //    if (settings == null)
    //    {
    //        var configPath = correspondenceSettings.Path ?? "unknown";
    //        throw new ArgumentException(
    //            $"Correspondence settings are required but could not be bound from configuration section '{configPath}'. " +
    //            $"Ensure the section exists and contains valid settings.", 
    //            nameof(correspondenceSettings));
    //    }

    //    services.AddSingleton<IDdNotificationSettings>(settings);

    //    var maskinportenOptions = BindMaskinportenSettings(maskinportenSettings);

    //    // Register the HttpClient with Maskinporten authentication and Polly-based resiliency
    //    services.AddMaskinportenHttpClient<TClientDefinition, IDdMessagingService, Services.DdMessagingService>(maskinportenOptions)
    //        .AddHttpMessageHandler(() => new AsyncPolicyDelegatingHandler(CreateRetryPolicy()));

    //    return services;
    //}

    //private static MaskinportenSettings BindMaskinportenSettings(IConfigurationSection configurationSection)
    //{
    //    if (configurationSection == null)
    //    {
    //        throw new ArgumentNullException(nameof(configurationSection), "Maskinporten settings configuration section cannot be null.");
    //    }

    //    var options = configurationSection.Get<MaskinportenSettings>();
    //    if (options == null)
    //    {
    //        throw new ArgumentException(
    //            $"Maskinporten settings are required but could not be bound from configuration section '{configurationSection.Path ?? "unknown"}'.",
    //            nameof(configurationSection));
    //    }

    //    if (string.IsNullOrWhiteSpace(options.ClientId))
    //    {
    //        throw new InvalidOperationException("Maskinporten ClientId must be provided.");
    //    }

    //    if (string.IsNullOrWhiteSpace(options.EncodedJwk))
    //    {
    //        throw new InvalidOperationException("Maskinporten EncodedJwk must be provided.");
    //    }

    //    if (string.IsNullOrWhiteSpace(options.Environment))
    //    {
    //        throw new InvalidOperationException("Maskinporten Environment must be provided.");
    //    }

    //    options.Scope = CorrespondenceScope;

    //    return options;
    //}

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
