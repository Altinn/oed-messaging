using Altinn.Dd.Correspondence.Features;
using Altinn.Dd.Correspondence.Features.Search;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Services;
using NSubstitute;

namespace Altinn.Dd.Correspondence.Tests;

/// <summary>
/// DdCorrespondenceService is a thin facade: each method forwards to its handler and returns the
/// handler's result untouched. These tests pin only that wiring - one handler per operation, no
/// crossed wires. The behaviour behind each handler is covered by the handler's own tests.
/// </summary>
public class DdCorrespondenceServiceTests
{
    private readonly IHandler<DdCorrespondenceDetails, CorrespondenceResult> _send =
        Substitute.For<IHandler<DdCorrespondenceDetails, CorrespondenceResult>>();

    private readonly IHandler<Query, Features.Search.Result> _search =
        Substitute.For<IHandler<Query, Features.Search.Result>>();

    private readonly IHandler<Features.Get.Request, Features.Get.Result> _get =
        Substitute.For<IHandler<Features.Get.Request, Features.Get.Result>>();

    private readonly DdCorrespondenceService _sut;

    public DdCorrespondenceServiceTests()
    {
        _sut = new DdCorrespondenceService(_send, _search, _get);
    }

    [Fact]
    public async Task SendCorrespondence_ForwardsToTheSendHandlerAlone()
    {
        var details = new DdCorrespondenceDetails { Recipient = "987654321" };
        var expected = CorrespondenceResult.Success(new ReceiptExternal(
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
        var expected = Features.Search.Result.Success([Guid.NewGuid()]);
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
        var request = new Features.Get.Request(Guid.NewGuid());
        var expected = Features.Get.Result.Failure("Not found");
        _get.Handle(request).Returns(expected);

        var result = await _sut.Get(request);

        Assert.Same(expected, result);
        await _get.Received(1).Handle(request);
        await _send.DidNotReceiveWithAnyArgs().Handle(default!);
        await _search.DidNotReceiveWithAnyArgs().Handle(default!);
    }
}
