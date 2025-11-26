using System.Text.Json;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Services.Interfaces;
using Altinn.Dd.Correspondence.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register DD Messaging Service with Maskinporten authentication
services.AddCorrespondenceClient(options =>
{
    configuration.GetSection("DdConfig").Bind(options);
});
    })
    .Build();

var messagingService = host.Services.GetRequiredService<IDdMessagingService>();

var messageDetails = new DdMessageDetails
{
    Recipient = "19838299493",
    Title = "Test Correspondence",
    Summary = "# Test Summary\nThis is a test summary in **markdown** format.",
    Body = "# Test Body\nThis is the main body content in **markdown** format.\n\n- Item 1\n- Item 2",
    Sender = "Test Sender",
    VisibleDateTime = null,
    ShipmentDatetime = null,
    Notification = new NotificationDetails
    {
        EmailSubject = "Test: ny melding i Altinn",
        EmailBody = "Hei. Du har mottatt en ny melding i Altinn. Logg inn for å lese den.",
        SmsText = "Du har en ny melding i Altinn. Logg inn for å lese."
    },
    AllowForwarding = false
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