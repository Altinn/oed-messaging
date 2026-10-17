using Altinn.Dd.Correspondence.Features;
using Altinn.Dd.Correspondence.Features.Get;
using Altinn.Dd.Correspondence.Features.Search;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Services;
using NSubstitute;

namespace Altinn.Dd.Correspondence.Tests.Services;

/// <summary>
/// DdCorrespondenceService is a thin facade: each method forwards to its handler and returns the
/// handler's result untouched. These tests pin that wiring - one handler per operation, no crossed
/// wires - and GetDialogId, the one method that reads the dialog id off the overview. The behaviour
/// behind each handler is covered by the handler's own tests.
/// </summary>
public class DdCorrespondenceServiceTests
{
    private readonly IHandler<DdCorrespondenceDetails, CorrespondenceResult<ReceiptExternal>> _send =
        Substitute.For<IHandler<DdCorrespondenceDetails, CorrespondenceResult<ReceiptExternal>>>();

    private readonly IHandler<Query, CorrespondenceResult<IEnumerable<Guid>>> _search =
        Substitute.For<IHandler<Query, CorrespondenceResult<IEnumerable<Guid>>>>();

    private readonly IHandler<Request, CorrespondenceResult<CorrespondenceOverview>> _get =
        Substitute.For<IHandler<Request, CorrespondenceResult<CorrespondenceOverview>>>();

    private readonly DdCorrespondenceService _sut;

    public DdCorrespondenceServiceTests()
    {
        _sut = new DdCorrespondenceService(_send, _search, _get);
    }

    [Fact]
    public async Task SendCorrespondence_ForwardsToTheSendHandlerAlone()
    {
        var details = new DdCorrespondenceDetails { Recipient = "987654321" };
        var expected = CorrespondenceResult<ReceiptExternal>.Success(new ReceiptExternal(
            new InitializedCorrespondences([], []), details.IdempotencyKey, "caller-reference"));
        _send.Handle(details).Returns(expected);

        var result = await _sut.SendCorrespondence(details);

        Assert.Same(expected, result);
        await _send.Received(1).Handle(details);
        await _search.DidNotReceiveWithAnyArgs().Handle(default!);
        await _get.DidNotReceiveWithAnyArgs().Handle(default!);
    }

    [Fact]
    public async Task Search_ForwardsToTheSearchHandlerAlone()
    {
        var query = new Query(ResourceId: "oed-correspondence", Role: CorrespondencesRoleType.Sender);
        var expected = CorrespondenceResult<IEnumerable<Guid>>.Success([Guid.NewGuid()]);
        _search.Handle(query).Returns(expected);

        var result = await _sut.Search(query);

        Assert.Same(expected, result);
        await _search.Received(1).Handle(query);
        await _send.DidNotReceiveWithAnyArgs().Handle(default!);
        await _get.DidNotReceiveWithAnyArgs().Handle(default!);
    }

    [Fact]
    public async Task Get_ForwardsToTheGetHandlerAlone()
    {
        var request = new Request(Guid.NewGuid());
        var expected = CorrespondenceResult<CorrespondenceOverview>.Failure("Not found");
        _get.Handle(request).Returns(expected);

        var result = await _sut.Get(request);

        Assert.Same(expected, result);
        await _get.Received(1).Handle(request);
        await _send.DidNotReceiveWithAnyArgs().Handle(default!);
        await _search.DidNotReceiveWithAnyArgs().Handle(default!);
    }

    // GetDialogId is the one method that does more than forward: it reads the dialog id off the
    // overview the get handler returns.

    private static CorrespondenceOverview AnOverview(params ExternalReference[] references) => new(
        ResourceId: "oed-correspondence",
        SendersReference: "reference",
        MessageSender: null,
        Content: null,
        RequestedPublishTime: null,
        AllowSystemDeleteAfter: null,
        DueDateTime: null,
        ExternalReferences: references,
        PropertyList: null,
        ReplyOptions: null,
        Notification: null,
        IgnoreReservation: null,
        Published: null,
        IsConfirmationNeeded: false,
        IsConfidential: false,
        Recipient: null,
        CorrespondenceId: Guid.NewGuid(),
        Created: DateTimeOffset.UtcNow,
        Status: Altinn.Dd.Correspondence.Features.Get.CorrespondenceStatus.Published,
        StatusText: null,
        StatusChanged: DateTimeOffset.UtcNow,
        Notifications: null,
        Altinn2CorrespondenceId: null);

    [Fact]
    public async Task GetDialogId_WhenTheDialogReferenceIsPresent_ReturnsItsId()
    {
        var request = new Request(Guid.NewGuid());
        var dialogId = Guid.NewGuid();
        _get.Handle(request).Returns(CorrespondenceResult<CorrespondenceOverview>.Success(AnOverview(
            new ExternalReference("instance", ReferenceType.AltinnAppInstance),
            new ExternalReference(dialogId.ToString(), ReferenceType.DialogportenDialogId))));

        var result = await _sut.GetDialogId(request);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(dialogId, result.Value);
        await _get.Received(1).Handle(request);
        await _send.DidNotReceiveWithAnyArgs().Handle(default!);
        await _search.DidNotReceiveWithAnyArgs().Handle(default!);
    }

    [Fact]
    public async Task GetDialogId_BeforeAltinnHasCreatedTheDialog_ReturnsNull()
    {
        var request = new Request(Guid.NewGuid());
        _get.Handle(request).Returns(CorrespondenceResult<CorrespondenceOverview>.Success(AnOverview()));

        var result = await _sut.GetDialogId(request);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetDialogId_WhenTheDialogReferenceIsNotAGuid_ReturnsNull()
    {
        var request = new Request(Guid.NewGuid());
        _get.Handle(request).Returns(CorrespondenceResult<CorrespondenceOverview>.Success(AnOverview(
            new ExternalReference("not-a-guid", ReferenceType.DialogportenDialogId))));

        var result = await _sut.GetDialogId(request);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetDialogId_WhenTheGetFails_ReturnsTheFailure()
    {
        var request = new Request(Guid.NewGuid());
        _get.Handle(request).Returns(CorrespondenceResult<CorrespondenceOverview>.Failure("Not found"));

        var result = await _sut.GetDialogId(request);

        Assert.True(result.IsFailure);
        Assert.Equal("Not found", result.Error);
    }

    [Fact]
    public async Task GetDialogId_WithSeveralDialogReferences_ReturnsTheFirstThatIsAGuid()
    {
        var request = new Request(Guid.NewGuid());
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _get.Handle(request).Returns(CorrespondenceResult<CorrespondenceOverview>.Success(AnOverview(
            new ExternalReference("not-a-guid", ReferenceType.DialogportenDialogId),
            new ExternalReference(first.ToString(), ReferenceType.DialogportenDialogId),
            new ExternalReference(second.ToString(), ReferenceType.DialogportenDialogId))));

        var result = await _sut.GetDialogId(request);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(first, result.Value);
    }

    [Fact]
    public async Task GetDialogId_WhenTheCorrespondenceFailed_ReturnsNull()
    {
        // Altinn fails the correspondence when it cannot create the dialog, so no dialog reference
        // is ever written. The id stays null rather than becoming a failure; callers that poll have
        // to check the status through Get to stop. Documented on GetDialogId.
        var request = new Request(Guid.NewGuid());
        _get.Handle(request).Returns(CorrespondenceResult<CorrespondenceOverview>.Success(
            AnOverview() with { Status = Altinn.Dd.Correspondence.Features.Get.CorrespondenceStatus.Failed }));

        var result = await _sut.GetDialogId(request);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetDialogId_WhenTheGetFails_PassesOnTheErrorCodeAndStatus()
    {
        var request = new Request(Guid.NewGuid());
        _get.Handle(request).Returns(CorrespondenceResult<CorrespondenceOverview>.Failure("The requested correspondence was not found", 1001, 404));

        var result = await _sut.GetDialogId(request);

        Assert.True(result.IsFailure);
        Assert.Equal("The requested correspondence was not found", result.Error);
        Assert.Equal(1001, result.ErrorCode);
        Assert.Equal(404, result.StatusCode);
    }
}
