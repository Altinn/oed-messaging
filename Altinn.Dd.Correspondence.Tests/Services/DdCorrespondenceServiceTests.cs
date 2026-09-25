using Altinn.Dd.Correspondence.Features;
using Altinn.Dd.Correspondence.Features.Get;
using Altinn.Dd.Correspondence.Features.Search;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Services;
using NSubstitute;

namespace Altinn.Dd.Correspondence.Tests.Services;

/// <summary>
/// DdCorrespondenceService is a thin facade: each method forwards to its handler and returns the
/// handler's result untouched. These tests pin only that wiring - one handler per operation, no
/// crossed wires. The behaviour behind each handler is covered by the handler's own tests.
/// </summary>
public class DdCorrespondenceServiceTests
{
    private readonly IHandler<DdCorrespondenceDetails, Result<ReceiptExternal>> _send =
        Substitute.For<IHandler<DdCorrespondenceDetails, Result<ReceiptExternal>>>();

    private readonly IHandler<Query, Result<IEnumerable<Guid>>> _search =
        Substitute.For<IHandler<Query, Result<IEnumerable<Guid>>>>();

    private readonly IHandler<Request, Result<CorrespondenceOverview>> _get =
        Substitute.For<IHandler<Request, Result<CorrespondenceOverview>>>();

    private readonly DdCorrespondenceService _sut;

    public DdCorrespondenceServiceTests()
    {
        _sut = new DdCorrespondenceService(_send, _search, _get);
    }

    [Fact]
    public async Task SendCorrespondence_ForwardsToTheSendHandlerAlone()
    {
        var details = new DdCorrespondenceDetails { Recipient = "987654321" };
        var expected = Result<ReceiptExternal>.Success(new ReceiptExternal(
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
        var expected = Result<IEnumerable<Guid>>.Success([Guid.NewGuid()]);
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
        var expected = Result<CorrespondenceOverview>.Failure("Not found");
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
        _get.Handle(request).Returns(Result<CorrespondenceOverview>.Success(AnOverview(
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
        _get.Handle(request).Returns(Result<CorrespondenceOverview>.Success(AnOverview()));

        var result = await _sut.GetDialogId(request);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetDialogId_WhenTheDialogReferenceIsNotAGuid_ReturnsNull()
    {
        var request = new Request(Guid.NewGuid());
        _get.Handle(request).Returns(Result<CorrespondenceOverview>.Success(AnOverview(
            new ExternalReference("not-a-guid", ReferenceType.DialogportenDialogId))));

        var result = await _sut.GetDialogId(request);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetDialogId_WhenTheGetFails_ReturnsTheFailure()
    {
        var request = new Request(Guid.NewGuid());
        _get.Handle(request).Returns(Result<CorrespondenceOverview>.Failure("Not found"));

        var result = await _sut.GetDialogId(request);

        Assert.True(result.IsFailure);
        Assert.Equal("Not found", result.Error);
    }
}
