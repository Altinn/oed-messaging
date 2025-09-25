using System.Text.Json;
using Altinn.Oed.Correspondence.Models;
using Altinn.Oed.Correspondence.Models.Interfaces;
using Altinn.Oed.Correspondence.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Altinn.Oed.Correspondence.Extensions;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

IOedNotificationSettings settings = configuration.GetSection("Settings").Get<Settings>()!;

// Use extension method for IHostBuilder for service registration 
var host = Host.CreateDefaultBuilder(args)
        .AddOedCorrespondence(settings)
        .Build();

var messagingService = host.Services.GetRequiredService<IOedMessagingService>();

var messageDetails = new OedMessageDetails
{
    Recipient = "983175155", // Test recipient
    Title = "Dette er melding fra Digitalt Dødsbo (Altinn 3)",
    Body = "Dette er en <strong>melding</strong> fra Altinn 3 Correspondence API",
    Sender = "Digitalt Dødsbo",
    VisibleDateTime = DateTime.Now.AddDays(7),
    Notification = new NotificationDetails
    {
        EmailBody = "Dette er email body fra Altinn 3",
        EmailSubject = "Dette er email subject fra Altinn 3",
        SmsText = "Dette er SMS fra Altinn 3"
    }
};

try
{
    var receipt = await messagingService.SendMessage(messageDetails);
    Console.WriteLine("✅ Correspondence sent successfully!");
    Console.WriteLine(JsonSerializer.Serialize(receipt, options: new JsonSerializerOptions
    {
        WriteIndented = true
    }));
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine($"Exception type: {ex.GetType().Name}");
}