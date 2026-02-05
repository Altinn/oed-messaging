using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Altinn.Dd.Correspondence.Services;
using Altinn.Dd.Correspondence.Options;
using Altinn.ApiClients.Maskinporten.Config;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Eksempel 1
        services.AddDdCorrespondenceService("DdConfig");

        // Eksempel 2
        //services.AddDdCorrespondenceService("NavnetPåKonsumentSeksjonIAppsettings", options =>
        //{
        //    var config = services.BuildServiceProvider().GetService<IConfiguration>();
        //    var ddConfig = config!.GetSection("DdConfig");
        //    var mpSettings = ddConfig.GetSection("MaskinportenSettings");
        //    options.MaskinportenSettings = new MaskinportenSettings
        //    {
        //        ClientId = mpSettings.GetValue<string>("ClientId"),
        //        EncodedJwk = mpSettings.GetValue<string>("EncodedJwk"),
        //        Environment = mpSettings.GetValue<string>("Environment"),
        //        EnableDebugLogging = mpSettings.GetValue<bool>("EnableDebugLogging")
        //    };
        //    options.ResourceId = ddConfig.GetValue<string>("ResourceId")!;
        //    options.Environment = ApiEnvironment.Staging;
        //});

        // Eksempel 3
        //services.AddDdCorrespondenceService(options =>
        //{
        //    var config = services.BuildServiceProvider().GetService<IConfiguration>();
        //    var ddConfig = config!.GetSection("DdConfig");
        //    var mpSettings = ddConfig.GetSection("MaskinportenSettings");
        //    options.MaskinportenSettings = new MaskinportenSettings
        //    {
        //        ClientId = mpSettings.GetValue<string>("ClientId"),
        //        EncodedJwk = mpSettings.GetValue<string>("EncodedJwk"),
        //        Environment = mpSettings.GetValue<string>("Environment"),
        //        EnableDebugLogging = mpSettings.GetValue<bool>("EnableDebugLogging")
        //    };
        //    options.ResourceId = ddConfig.GetValue<string>("ResourceId")!;
        //    options.Environment = ApiEnvironment.Staging;
        //});
    })
    .Build();

var messagingService = host.Services.GetRequiredService<IDdCorrespondenceService>();

var messageDetails = new DdCorrespondenceDetails
{
    Recipient = "05916896346",
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
    AllowForwarding = false,
    IgnoreReservation = true
};

try
{
    // 1 måte
    var receipt = await messagingService.SendCorrespondence(messageDetails);
    var result = receipt.Match(
        onSuccess: receipt => $"Woho {receipt.IdempotencyKey}",
        onFailure: error => $"Buhu {error}");
    Console.WriteLine(result);

    // 2 måte
    //var receipt2 = await messagingService.SendCorrespondence(messageDetails);
    //if (receipt2.IsSuccess)
    //{
    //    Console.WriteLine($"Woho {receipt2.Receipt!.IdempotencyKey}");
    //}
    //else if (receipt2.IsFailure)
    //{
    //    Console.WriteLine($"Buhu {receipt2.Error}");
    //}
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}