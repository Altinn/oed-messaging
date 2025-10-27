using Altinn.Dd.Correspondence.Authentication;
using Altinn.Dd.Correspondence.Models.Interfaces;
using Altinn.Dd.Correspondence.Services;
using Altinn.Dd.Correspondence.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace Altinn.Dd.Correspondence.Extensions;

public static class HostBuilderExtensions
{
    /// <summary>
    /// Creates a retry policy for HTTP requests to handle transient failures
    /// </summary>
    /// <returns>A retry policy</returns>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempts (this will be handled by the logger in the service)
                });
    }

    /// <summary>
    /// Adds DD Correspondence services to the host builder.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure</param>
    /// <param name="settings">The correspondence settings</param>
    /// <param name="accessTokenProvider">The token provider for authentication</param>
    /// <returns>The configured host builder</returns>
    public static IHostBuilder AddDdCorrespondence(
        this IHostBuilder hostBuilder, 
        IDdNotificationSettings settings,
        IAccessTokenProvider accessTokenProvider)
    {
        hostBuilder.ConfigureServices(serviceCollection =>
        {
            // Register the token provider
            serviceCollection.AddSingleton(accessTokenProvider);
            
            // Register the authentication handler
            serviceCollection.AddTransient<BearerTokenHandler>();
            
            // Register HttpClient with authentication handler and retry policy
            serviceCollection
                .AddHttpClient<IDdMessagingService, DdMessagingService>()
                .AddHttpMessageHandler<BearerTokenHandler>()
                .AddPolicyHandler(GetRetryPolicy());
            
            // Override the registration to make it singleton
            serviceCollection.AddSingleton<IDdMessagingService>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(IDdMessagingService));
                var logger = sp.GetRequiredService<ILogger<Services.DdMessagingService>>();
                return new Services.DdMessagingService(httpClient, settings, logger);
            });
            
            // Register settings
            serviceCollection.AddSingleton(_ => settings);
        });

        return hostBuilder;
    }
}
