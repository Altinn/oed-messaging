using Altinn.Oed.Messaging.Models.Interfaces;
using Altinn.Oed.Messaging.Services;
using Altinn.Oed.Messaging.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Altinn.Oed.Messaging.Extensions;
public static class HostBuilderExtensions
{
    public static IHostBuilder AddOedMessaging(this IHostBuilder hostBuilder, IOedNotificationSettings settings)
    {
        hostBuilder.ConfigureServices(serviceCollection =>
            serviceCollection
                .AddSingleton<IChannelManagerService, ChannelManagerService>()
                .AddSingleton<IOedMessagingService, OedMessagingService>()
                .AddSingleton(_ => settings));

        return hostBuilder;
    }
}
