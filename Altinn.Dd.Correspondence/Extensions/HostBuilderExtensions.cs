using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Altinn.ApiClients.Maskinporten.Config;
using Altinn.ApiClients.Maskinporten.Extensions;
using Altinn.ApiClients.Maskinporten.Interfaces;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Polly;

namespace Altinn.Dd.Correspondence.Extensions;

public static class ServiceCollectionExtensions
{
    private const string CorrespondenceScope = "altinn:serviceowner altinn:correspondence.write";
    private const string ClientDefinitionKey = "correspondence-dd-sdk";
    private const int RetryCount = 3;

    /// <summary>
    /// Adds DD Correspondence client with Maskinporten authentication.
    /// This method follows the Dialogporten pattern for service registration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="settings">The correspondence settings including Maskinporten configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCorrespondenceClient(this IServiceCollection services, CorrespondenceSettings settings)
    {
        if (!CorrespondenceSettings.Validate(settings))
        {
            throw new InvalidOperationException("Invalid correspondence configuration");
        }

        services.TryAddSingleton<IOptions<CorrespondenceSettings>>(new OptionsWrapper<CorrespondenceSettings>(settings));

        // Configure Maskinporten scope for correspondence
        settings.Maskinporten.Scope = CorrespondenceScope;

        services.RegisterMaskinportenClientDefinition<SettingsJwkClientDefinition>(ClientDefinitionKey, settings.Maskinporten);

        services.AddHttpClient<IDdMessagingService, Services.DdMessagingService>()
            .ConfigureHttpClient(client => client.BaseAddress = new Uri(settings.BaseUri))
            .AddMaskinportenHttpMessageHandler<SettingsJwkClientDefinition>(ClientDefinitionKey)
            .AddHttpMessageHandler(() => new AsyncPolicyDelegatingHandler(CreateRetryPolicy()));

        return services;
    }

    /// <summary>
    /// Adds DD Correspondence client with Maskinporten authentication using action-based configuration.
    /// This method follows the Dialogporten pattern for service registration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure correspondence settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCorrespondenceClient(this IServiceCollection services, Action<CorrespondenceSettings> configureOptions)
    {
        var correspondenceSettings = new CorrespondenceSettings
        {
            Maskinporten = new MaskinportenSettings()
        };
        configureOptions.Invoke(correspondenceSettings);
        return services.AddCorrespondenceClient(correspondenceSettings);
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
