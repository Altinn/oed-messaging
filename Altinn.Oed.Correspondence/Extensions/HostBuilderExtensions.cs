using Altinn.Oed.Correspondence.Authentication;
using Altinn.Oed.Correspondence.Models.Interfaces;
using Altinn.Oed.Correspondence.Services;
using Altinn.Oed.Correspondence.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Altinn.Oed.Correspondence.Extensions;

public static class HostBuilderExtensions
{
    /// <summary>
    /// Adds OED Correspondence services to the host builder.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure</param>
    /// <param name="settings">The correspondence settings</param>
    /// <param name="accessTokenProvider">The token provider for authentication</param>
    /// <returns>The configured host builder</returns>
    public static IHostBuilder AddOedCorrespondence(
        this IHostBuilder hostBuilder, 
        IOedNotificationSettings settings,
        IAccessTokenProvider accessTokenProvider)
    {
        hostBuilder.ConfigureServices(serviceCollection =>
        {
            // Register the token provider
            serviceCollection.AddSingleton(accessTokenProvider);
            
            // Register the authentication handler
            serviceCollection.AddTransient<BearerTokenHandler>();
            
            // Register HttpClient with authentication handler
            serviceCollection
                .AddHttpClient<IOedMessagingService, OedMessagingService>()
                .AddHttpMessageHandler<BearerTokenHandler>();
            
            serviceCollection.AddSingleton<IOedMessagingService>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(IOedMessagingService));
                return new OedMessagingService(httpClient, settings);
            });
            
            // Register settings
            serviceCollection.AddSingleton(_ => settings);
        });

        return hostBuilder;
    }
}
