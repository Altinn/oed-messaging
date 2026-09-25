using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Tests.TestSupport;
using System.Net;

using CorrespondenceSend = Altinn.Dd.Correspondence.Features.Send;

namespace Altinn.Dd.Correspondence.Tests.Features;

/// <summary>
/// Covers the logic in CorrespondenceSend.Handler - recipient formatting, notification assembly and
/// error translation - by inspecting the request the handler actually puts on the wire.
/// </summary>
public class SendHandlerTests
{
    private static InitializeCorrespondencesResponseExt AnAcceptedResponse() => new()
    {
        Correspondences = [new InitializedCorrespondencesExt
        {
            CorrespondenceId = Guid.NewGuid(),
            Status = CorrespondenceStatusExt.Initialized,
            Recipient = "recipient"
        }],
        AttachmentIds = []
    };

    private static DdCorrespondenceDetails Details(string recipient = "987654321") => new()
    {
        Recipient = recipient,
        Title = "Title",
        Summary = "Summary",
        Body = "Body",
        Sender = "Sender",
        Notification = null
    };

    private static async Task<(Result<ReceiptExternal> Result, HandlerHarness Harness)> Send(DdCorrespondenceDetails details)
    {
        var harness = new HandlerHarness().RespondsWith(HttpMethod.Post, AnAcceptedResponse());
        var sut = new CorrespondenceSend.Handler(harness.Client(), harness.Options());

        return (await sut.Handle(details), harness);
    }

    [Theory]
    // A bare 9-digit organization number gets the ISO 6523 country code Altinn 3 expects.
    [InlineData("987654321", "0192:987654321")]
    // Anything already addressed is forwarded untouched.
    [InlineData("0192:987654321", "0192:987654321")]
    [InlineData("urn:altinn:organization:identifier-no:987654321", "urn:altinn:organization:identifier-no:987654321")]
    [InlineData("URN:ALTINN:person:identifier-no:01010112345", "URN:ALTINN:person:identifier-no:01010112345")]
    // An 11-digit national identity number is not 9 digits, so it is passed through as-is.
    [InlineData("01010112345", "01010112345")]
    public async Task Recipient_IsFormattedForAltinn3(string recipient, string expected)
    {
        var (result, harness) = await Send(Details(recipient));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal([expected], harness.SentCorrespondence().Recipients);
    }

    [Fact]
    public async Task Notification_WhenNotProvided_NoneIsRequested()
    {
        var (_, harness) = await Send(Details());

        Assert.Null(harness.SentCorrespondence().Correspondence.Notification);
    }

    [Fact]
    public async Task Notification_WhenDetailsArePresentButEmpty_NoneIsRequested()
    {
        // DdCorrespondenceDetails.Notification defaults to an empty instance, so an untouched
        // default must not turn into an empty notification request.
        var details = Details();
        details.Notification = new NotificationDetails();

        var (_, harness) = await Send(details);

        Assert.Null(harness.SentCorrespondence().Correspondence.Notification);
    }

    [Fact]
    public async Task Notification_WithEmailOnly_UsesEmailChannel()
    {
        var details = Details();
        details.Notification = new NotificationDetails
        {
            EmailSubject = "Subject",
            EmailBody = "Body",
            EmailContentType = EmailContentType.Html
        };

        var (_, harness) = await Send(details);

        var notification = harness.SentCorrespondence().Correspondence.Notification;
        Assert.NotNull(notification);
        Assert.Equal(NotificationChannelExt.Email, notification!.NotificationChannel);
        Assert.Equal("Subject", notification.EmailSubject);
        Assert.Equal(HttpClients.EmailContentType.Html, notification.EmailContentType);
        Assert.Null(notification.SmsBody);
    }

    [Fact]
    public async Task Notification_WithSmsOnly_UsesSmsChannel()
    {
        var details = Details();
        details.Notification = new NotificationDetails { SmsText = "Sms" };

        var (_, harness) = await Send(details);

        var notification = harness.SentCorrespondence().Correspondence.Notification;
        Assert.NotNull(notification);
        Assert.Equal(NotificationChannelExt.Sms, notification!.NotificationChannel);
        Assert.Equal("Sms", notification.SmsBody);
    }

    [Fact]
    public async Task Notification_WithEmailAndSms_UsesCombinedChannel()
    {
        var details = Details();
        details.Notification = new NotificationDetails
        {
            EmailSubject = "Subject",
            EmailBody = "Body",
            SmsText = "Sms"
        };

        var (_, harness) = await Send(details);

        var notification = harness.SentCorrespondence().Correspondence.Notification;
        Assert.NotNull(notification);
        Assert.Equal(NotificationChannelExt.EmailAndSms, notification!.NotificationChannel);
    }

    [Fact]
    public async Task Notification_WithEmailSubjectButNoBody_IsNotTreatedAsAnEmailNotification()
    {
        var details = Details();
        details.Notification = new NotificationDetails { EmailSubject = "Subject", SmsText = "Sms" };

        var (_, harness) = await Send(details);

        var notification = harness.SentCorrespondence().Correspondence.Notification;
        Assert.NotNull(notification);
        Assert.Equal(NotificationChannelExt.Sms, notification!.NotificationChannel);
        Assert.Null(notification.EmailSubject);
    }

    [Fact]
    public async Task SendersReference_WhenNotProvided_IsDerivedFromTheIdempotencyKey()
    {
        var details = Details();
        details.SendersReference = null;

        var (result, harness) = await Send(details);

        var expected = $"EXT_DD_SHIP_{details.IdempotencyKey}";
        Assert.Equal(expected, harness.SentCorrespondence().Correspondence.SendersReference);
        Assert.Equal(expected, result.Value!.SendersReference);
    }

    [Fact]
    public async Task SendersReference_WhenProvided_IsUsedAndEchoedOnTheReceipt()
    {
        var details = Details();
        details.SendersReference = "caller-reference";

        var (result, harness) = await Send(details);

        Assert.Equal("caller-reference", harness.SentCorrespondence().Correspondence.SendersReference);
        Assert.Equal("caller-reference", result.Value!.SendersReference);
    }

    [Fact]
    public async Task IdempotencyKeyAndResourceId_AreForwarded()
    {
        var details = Details();

        var (result, harness) = await Send(details);

        var sent = harness.SentCorrespondence();
        Assert.Equal(details.IdempotencyKey, sent.IdempotentKey);
        Assert.Equal(HandlerHarness.ResourceId, sent.Correspondence.ResourceId);
        Assert.Equal(details.IdempotencyKey, result.Value!.IdempotencyKey);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IgnoreReservation_IsForwarded(bool ignoreReservation)
    {
        var details = Details();
        details.IgnoreReservation = ignoreReservation;

        var (_, harness) = await Send(details);

        Assert.Equal(ignoreReservation, harness.SentCorrespondence().Correspondence.IgnoreReservation);
    }

    [Fact]
    public async Task Send_WhenApiRejectsTheRequest_ReturnsFailureCarryingTheProblemDetail()
    {
        using var harness = new HandlerHarness()
            .RespondsWithProblem(HttpMethod.Post, HttpStatusCode.BadRequest, "1020: Message title cannot be empty");
        var sut = new CorrespondenceSend.Handler(harness.Client(), harness.Options());

        var result = await sut.Handle(Details());

        Assert.True(result.IsFailure);
        Assert.Equal("1020: Message title cannot be empty", result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Send_WhenTheIdempotencyKeyIsAlreadyUsed_Throws()
    {
        // The handler only catches AltinnCorrespondenceException<ProblemDetails>, and the generated
        // client raises the non-generic exception for 409. A duplicate send therefore escapes as an
        // exception instead of a failure result - documented in the package README.
        using var harness = new HandlerHarness()
            .RespondsWithProblem(HttpMethod.Post, HttpStatusCode.Conflict, "1034: duplicate idempotent key");
        var sut = new CorrespondenceSend.Handler(harness.Client(), harness.Options());

        var exception = await Assert.ThrowsAsync<AltinnCorrespondenceException>(() => sut.Handle(Details()));

        Assert.Equal((int)HttpStatusCode.Conflict, exception.StatusCode);
    }

    [Fact]
    public async Task DialogId_WhenNotProvided_NoExternalReferencesAreSent()
    {
        var (_, harness) = await Send(Details());

        Assert.Null(harness.SentCorrespondence().Correspondence.ExternalReferences);
    }

    [Fact]
    public async Task DialogId_WhenProvided_IsSentAsADialogportenDialogIdReference()
    {
        var dialogId = Guid.NewGuid();
        var details = Details();
        details.DialogId = dialogId;

        var (result, harness) = await Send(details);

        Assert.True(result.IsSuccess, result.Error);
        var reference = Assert.Single(harness.SentCorrespondence().Correspondence.ExternalReferences);
        Assert.Equal(ReferenceTypeExt.DialogportenDialogId, reference.ReferenceType);
        Assert.Equal(dialogId.ToString(), reference.ReferenceValue);
    }

    [Fact]
    public async Task TransmissionType_WithDialogId_IsSentByNameAlongsideTheDialogReference()
    {
        var dialogId = Guid.NewGuid();
        var details = Details();
        details.DialogId = dialogId;
        details.TransmissionType = TransmissionType.Decision;

        var (result, harness) = await Send(details);

        Assert.True(result.IsSuccess, result.Error);
        var references = harness.SentCorrespondence().Correspondence.ExternalReferences;
        Assert.Collection(references,
            reference =>
            {
                Assert.Equal(ReferenceTypeExt.DialogportenDialogId, reference.ReferenceType);
                Assert.Equal(dialogId.ToString(), reference.ReferenceValue);
            },
            reference =>
            {
                Assert.Equal(ReferenceTypeExt.DialogportenTransmissionType, reference.ReferenceType);
                Assert.Equal("Decision", reference.ReferenceValue);
            });
        // Altinn reads the reference type by name, so the new member must go out as a string.
        Assert.Contains("\"DialogportenTransmissionType\"", harness.LastRequestBody);
    }

    [Fact]
    public async Task TransmissionType_WithoutDialogId_FailsWithoutCallingAltinn()
    {
        var details = Details();
        details.TransmissionType = TransmissionType.Request;

        var (result, harness) = await Send(details);

        Assert.True(result.IsFailure);
        Assert.Equal(CorrespondenceSend.Handler.TransmissionTypeWithoutDialogId, result.Error);
        Assert.Equal(0, harness.RequestCount);
    }
}
