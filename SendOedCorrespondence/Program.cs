using System.Text.Json;
using Altinn.ApiClients.Maskinporten.Services;
using Altinn.ApiClients.Maskinporten.Interfaces;
using Altinn.Oed.Correspondence.Authentication;
using Altinn.Oed.Correspondence.Models;
using Altinn.Oed.Correspondence.Models.Interfaces;
using Altinn.Oed.Correspondence.Services;
using Altinn.Oed.Correspondence.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SendOedCorrespondence.Authentication;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();

// Load settings
var settings = configuration.GetSection("Settings").Get<Settings>()!;
var maskinportenSettings = configuration.GetSection("MaskinportenSettings");

// Configure and build host with all services
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register Maskinporten services
        services.AddHttpClient<IMaskinportenService, MaskinportenService>();
        services.AddMemoryCache();
        services.AddSingleton<ITokenCacheProvider, MemoryTokenCacheProvider>();

        // Create token provider adapter
        services.AddSingleton<IAccessTokenProvider>(sp =>
        {
            var maskinportenService = sp.GetRequiredService<IMaskinportenService>();
            var logger = sp.GetRequiredService<ILogger<MaskinportenTokenAdapter>>();
            return new MaskinportenTokenAdapter(maskinportenService, maskinportenSettings, logger);
        });

        // Register correspondence services
        services.AddSingleton<IOedNotificationSettings>(settings);
        services.AddTransient<BearerTokenHandler>();
        services.AddHttpClient<IOedMessagingService, OedMessagingService>()
            .AddHttpMessageHandler<BearerTokenHandler>();
    })
    .Build();

var messagingService = host.Services.GetRequiredService<IOedMessagingService>();

var messageDetails = new OedMessageDetails
{
    Recipient = "983175155", // Test recipient
    Title = "Test", // Minimal plain text
    Summary = "Test summary", // Minimal text
    Body = "Test body", // Minimal text
    Sender = "Test Sender",
    VisibleDateTime = DateTime.Now.AddDays(7)
    // No notification at all for now to test basic correspondence
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