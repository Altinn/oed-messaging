using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Altinn.Dd.Correspondence.Services;
using Altinn.Dd.Correspondence.Features.Search;
using Altinn.Dd.Correspondence.HttpClients;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config => config.AddUserSecrets<Program>())
    // The HttpClient and Polly pipelines log every request and attempt at Information, which buries
    // the demo's own output. Keep warnings and errors.
    .ConfigureLogging(logging => logging
        .AddFilter("Microsoft", LogLevel.Warning)
        .AddFilter("System", LogLevel.Warning)
        .AddFilter("Polly", LogLevel.Warning))
    .ConfigureServices(services =>
    {
        // Eksempel 1
        services.AddDdCorrespondenceService("DdConfig");
        services.AddHttpClient("AltinnCorrespondenceClient")
            .AddHttpMessageHandler(() => new PrintSendResponseHandler());

        // Eksempel 2 og 3 trenger disse i tillegg:
        //   using Altinn.ApiClients.Maskinporten.Config;
        //   using Altinn.Dd.Correspondence.Options;
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
        //
        //    options.ResourceId = ddConfig.GetValue<string>("ResourceId")!;
        //    options.Environment = ApiEnvironment.Staging;
        //    options.MaskinportenSettings = new MaskinportenSettings
        //    {
        //        ClientId = mpSettings.GetValue<string>("ClientId"),
        //        EncodedJwk = mpSettings.GetValue<string>("EncodedJwk"),
        //        Environment = mpSettings.GetValue<string>("Environment"),
        //        EnableDebugLogging = mpSettings.GetValue<bool>("EnableDebugLogging")
        //    };
        //});
    })
    .Build();

var messagingService = host.Services.GetRequiredService<IDdCorrespondenceService>();

var messageDetails = new DdCorrespondenceDetails
{
    Recipient = "21890049793",
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
    IgnoreReservation = true,
    IdempotencyKey = Guid.NewGuid(),
    SendersReference = "sender_ref_123"
};

// Dialog demo, run with: dotnet run -- dialog
if (args.Contains("dialog"))
{
    await RunDialogDemo(messagingService, messageDetails.Recipient!);
    return;
}

try
{
    // 1 måte
    //var receipt = await messagingService.SendCorrespondence(messageDetails);
    //var result = receipt.Match(
    //    onSuccess: receipt => $"Woho {receipt.IdempotencyKey}",
    //    onFailure: error => $"Buhu {error}");
    //Console.WriteLine(result);

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

    // Example Send, Search and Get
    var sendResult = await messagingService.SendCorrespondence(messageDetails);
    if (sendResult.IsSuccess)
    {
        Console.WriteLine($"Send succeeded: {sendResult.Value!.SendersReference}");
        var query = new Query(
            Role: CorrespondencesRoleType.Sender,
            ResourceId: "oed-correspondence", 
            SendersReference: sendResult.Value!.SendersReference);

        var searchResult = await messagingService.Search(query);
        if (searchResult.IsSuccess)
        {
            Console.WriteLine($"Search succeeded: {searchResult.Value!.First()}");
            var getResult = await messagingService.Get(new Altinn.Dd.Correspondence.Features.Get.Request(searchResult.Value!.First()));
            if (getResult.IsSuccess)
            {
                Console.WriteLine($"Get succeeded: {getResult.Value!.StatusText}");
            }
        }

    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex}");
}

// Sends one correspondence, which creates a new Dialogporten dialog, waits for Altinn to create
// that dialog, and then adds one transmission of every TransmissionType to it.
static async Task RunDialogDemo(IDdCorrespondenceService messagingService, string recipient)
{
    var runReference = $"dialog-demo-{DateTime.Now:yyyyMMdd-HHmmss}";

    try
    {
        // 1. The first correspondence creates the dialog. No notifications, so the demo does not
        //    send nine e-mails and text messages.
        var rootResult = await messagingService.SendCorrespondence(new DdCorrespondenceDetails
        {
            Recipient = recipient,
            Title = "Dialogdemo: første melding",
            Summary = "Denne meldingen oppretter dialogen.",
            Body = "# Første melding\nDenne meldingen oppretter dialogen i Dialogporten.",
            Sender = "Test Sender",
            Notification = null,
            IgnoreReservation = true,
            SendersReference = runReference
        });
        if (rootResult.IsFailure)
        {
            Console.WriteLine($"Root send failed: {rootResult.Error}");
            return;
        }

        var rootId = rootResult.Value!.InitalizedCorrespondences.Correspondences.Single().CorrespondenceId;
        Console.WriteLine($"Root correspondence sent: {rootId}");

        // 2. Altinn creates the dialog in the background after the correspondence is published, so
        //    the id is not in the receipt. Poll until it shows up.
        Guid? dialogId = null;
        var deadline = DateTime.UtcNow.AddMinutes(3);
        while (dialogId is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            var dialogResult = await messagingService.GetDialogId(new Altinn.Dd.Correspondence.Features.Get.Request(rootId));
            if (dialogResult.IsFailure)
            {
                Console.WriteLine($"GetDialogId failed: {dialogResult.Error}");
                return;
            }

            dialogId = dialogResult.Value;
            Console.WriteLine(dialogId is null ? "Waiting for the dialog..." : $"Dialog created: {dialogId}");
        }

        if (dialogId is null)
        {
            Console.WriteLine("Gave up: Altinn did not create the dialog within 3 minutes.");
            return;
        }

        // 3. One transmission of each type on the same dialog.
        foreach (var transmissionType in Enum.GetValues<TransmissionType>())
        {
            var result = await messagingService.SendCorrespondence(new DdCorrespondenceDetails
            {
                Recipient = recipient,
                Title = $"Dialogdemo: {transmissionType}",
                Summary = $"En melding av typen {transmissionType}.",
                Body = $"# {transmissionType}\nDenne meldingen er lagt til i dialogen som en {transmissionType}-forsendelse.",
                Sender = "Test Sender",
                Notification = null,
                IgnoreReservation = true,
                SendersReference = runReference,
                DialogId = dialogId,
                TransmissionType = transmissionType
            });

            Console.WriteLine(result.IsSuccess
                ? $"{transmissionType}: sent {result.Value!.InitalizedCorrespondences.Correspondences.Single().CorrespondenceId}"
                : $"{transmissionType}: failed: {result.Error}");
        }

        Console.WriteLine($"Done. Every correspondence in this run has SendersReference {runReference}.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex}");
    }
}

// Prints the raw JSON body Altinn returns for the send (POST) request.
internal class PrintSendResponseHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (request.Method == HttpMethod.Post)
        {
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var pretty = System.Text.Json.JsonSerializer.Serialize(
                System.Text.Json.JsonDocument.Parse(body).RootElement,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"Send response ({(int)response.StatusCode}):\n{pretty}");
        }
        return response;
    }
}
