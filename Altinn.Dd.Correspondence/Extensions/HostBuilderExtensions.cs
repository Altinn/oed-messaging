using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Interfaces;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Models.Interfaces;
using Altinn.Dd.Correspondence.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Dd.Correspondence.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds DD Messaging Service with Maskinporten authentication to the service collection.
    /// This method follows the Altinn 3 pattern for registering HttpClients with Maskinporten.
    /// </summary>
    /// <typeparam name="TClientDefinition">The Maskinporten client definition type (e.g., SettingsJwkClientDefinition)</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="maskinportenSettings">Configuration section containing Maskinporten settings (must include ClientId)</param>
    /// <param name="correspondenceSettings">Configuration section containing correspondence settings</param>
    /// <param name="configureClient">Optional action to configure the Maskinporten client (e.g., set EnableDebugLogging)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDdMessagingService<TClientDefinition>(
        this IServiceCollection services,
        IConfigurationSection maskinportenSettings,
        IConfigurationSection correspondenceSettings,
        Action<dynamic>? configureClient = null)
        where TClientDefinition : class, IClientDefinition
    {
        // Bind and register correspondence settings
        var settings = correspondenceSettings.Get<Settings>();
        if (settings == null)
        {
            throw new ArgumentException("Correspondence settings are required", nameof(correspondenceSettings));
        }
        
        services.AddSingleton<IDdNotificationSettings>(settings);
        
        // Register the HttpClient with Maskinporten authentication
        services.AddMaskinportenHttpClient<TClientDefinition, IDdMessagingService, Services.DdMessagingService>(
            maskinportenSettings,
            clientDefinition =>
            {
                // Consumers don't need to specify the scope because it will be the same for all correspondence API calls
                ((dynamic)clientDefinition).ClientSettings.Scope = "altinn:serviceowner altinn:correspondence.write";
                
                // Allow consumers to configure additional settings (e.g., EnableDebugLogging)
                configureClient?.Invoke(clientDefinition);
            });
        
        return services;
    }
}
