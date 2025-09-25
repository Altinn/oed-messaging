using Altinn.Oed.Correspondence.Models.Interfaces;
using Altinn.Oed.Correspondence.Services;
using Altinn.Oed.Correspondence.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Altinn.Oed.Correspondence.Extensions;
public static class HostBuilderExtensions
{
    public static IHostBuilder AddOedCorrespondence(this IHostBuilder hostBuilder, IOedNotificationSettings settings)
    {
        hostBuilder.ConfigureServices(serviceCollection =>
            serviceCollection
                .AddHttpClient()
                .AddSingleton<IOedMessagingService>(provider =>
                {
                    var httpClient = provider.GetRequiredService<HttpClient>();
                    return new OedMessagingService(httpClient, settings);
                })
                .AddSingleton(_ => settings));

        return hostBuilder;
    }
}
