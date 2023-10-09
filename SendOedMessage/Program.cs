using System.Text.Json;
using Altinn.Oed.Messaging.Models;
using Altinn.Oed.Messaging.Models.Interfaces;
using Altinn.Oed.Messaging.Services;
using Altinn.Oed.Messaging.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Altinn.Oed.Messaging.Extensions;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

IOedNotificationSettings settings = configuration.GetSection("Settings").Get<Settings>()!;

// Use extension method for IHostBuilder for service registration 
var host = Host.CreateDefaultBuilder(args)
        .AddOedMessaging(settings)
        //.ConfigureServices(serviceCollection => ...)
        .Build();

var messagingService = host.Services.GetRequiredService<IOedMessagingService>();

/*
// Service registration without IHostBuilder
var builder = new ServiceCollection()
    .AddSingleton(_ => settings)
    .AddSingleton<IChannelManagerService, ChannelManagerService>()
    .AddSingleton<IOedMessagingService, OedMessagingService>()
    .BuildServiceProvider();

var messagingService = builder.GetRequiredService<IOedMessagingService>();
*/

/* 
// Manual instantiation
var channelManagerService = new ChannelManagerService(settings);
var messagingService = new OedMessagingService(channelManagerService, settings);
*/

var messageDetails = new OedMessageDetails
{
    Recipient = "983175155", // DAGL=22925498622 
    Title = "Dette er melding fra Digitalt Dødsbo",
    //Summary = "Dette er sammendraget som vises over streken i meldingen (men etter ekspandering).",
    Body = "Dette er en <strong>melding</strong>",
    Sender = "Digitalt Dødsbo",
    VisibleDateTime = DateTime.Now.AddDays(7),
    Notification = new NotificationDetails
    {
        EmailBody = "Dette er body",
        EmailSubject = "Dette er subject",
        SmsText = "Dette er SMS"
    }
};

var receipt = await messagingService.SendMessage(messageDetails);
Console.WriteLine(JsonSerializer.Serialize(receipt, options: new JsonSerializerOptions
{
    WriteIndented = true
}));
