using Altinn.Dd.Correspondence.Features;
using Altinn.Dd.Correspondence.Features.Search;
using Altinn.Dd.Correspondence.Models;
using Altinn.Dd.Correspondence.Services;
using NSubstitute;

namespace Altinn.Oed.Correspondence.Tests.Services;

public class DdCorrespondenceServiceTests
{
    private readonly IHandler<DdCorrespondenceDetails, CorrespondenceResult> _send;
    private readonly IHandler<Query, Dd.Correspondence.Features.Search.Result> _search;
    private readonly IHandler<Dd.Correspondence.Features.Get.Request, Dd.Correspondence.Features.Get.Result> _get;
    private readonly DdCorrespondenceService _sut;

    public DdCorrespondenceServiceTests()
    {
        _send = Substitute.For<IHandler<DdCorrespondenceDetails, CorrespondenceResult>>();
        _search = Substitute.For<IHandler<Query, Dd.Correspondence.Features.Search.Result>>();
        _get = Substitute.For<IHandler<Dd.Correspondence.Features.Get.Request, Dd.Correspondence.Features.Get.Result>>();

        _sut = new DdCorrespondenceService(_send, _search, _get);
    }

    [Fact]
    public async Task SendCorrespondence_WithValidDetails_ShouldSucceed()
    {
        var recipient = "test-recipient";
        var correspondence = ValidCorrespondenceDetails(recipient);
        var expectedReceipt = new ReceiptExternal(
            InitalizedCorrespondences: null,
            IdempotencyKey: correspondence.IdempotencyKey,
            SendersReference: correspondence.SendersReference);

        _send.Handle(correspondence).Returns(CorrespondenceResult.Success(expectedReceipt));

        var result = await _sut.SendCorrespondence(correspondence);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Receipt);
        Assert.Equal(correspondence.IdempotencyKey, result.Receipt.IdempotencyKey);
        Assert.Equal(correspondence.SendersReference, result.Receipt.SendersReference);
    }

    [Fact]
    public async Task SendCorrespondence_400BadRequest_FailureResult()
    {
        var correspondence = ValidCorrespondenceDetails(string.Empty);
        _send.Handle(correspondence).Returns(CorrespondenceResult.Failure("Bad request"));

        var result = await _sut.SendCorrespondence(correspondence);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Search_ShouldInvokeHandlerAndReturnResult()
    {
        var query = new Query();
        var expected = Dd.Correspondence.Features.Search.Result.Success([Guid.NewGuid()]);
        _search.Handle(query).Returns(expected);

        var result = await _sut.Search(query);

        Assert.Same(expected, result);
        await _search.Received(1).Handle(query);
    }

    [Fact]
    public async Task Search_WhenHandlerReturnsFailure_ShouldReturnSameFailure()
    {
        var query = new Query();
        var expected = Dd.Correspondence.Features.Search.Result.Failure("Search failed");
        _search.Handle(query).Returns(expected);

        var result = await _sut.Search(query);

        Assert.Same(expected, result);
        Assert.True(result.IsFailure);
        Assert.Equal("Search failed", result.Error);
    }

    [Fact]
    public async Task Get_ShouldInvokeHandlerAndReturnResult()
    {
        var request = new Dd.Correspondence.Features.Get.Request(Guid.NewGuid());
        var expected = Dd.Correspondence.Features.Get.Result.Success(null);
        _get.Handle(request).Returns(expected);

        var result = await _sut.Get(request);

        Assert.Same(expected, result);
        await _get.Received(1).Handle(request);
    }

    [Fact]
    public async Task Get_WhenHandlerReturnsFailure_ShouldReturnSameFailure()
    {
        var request = new Dd.Correspondence.Features.Get.Request(Guid.NewGuid());
        var expected = Dd.Correspondence.Features.Get.Result.Failure("Not found");
        _get.Handle(request).Returns(expected);

        var result = await _sut.Get(request);

        Assert.Same(expected, result);
        Assert.True(result.IsFailure);
        Assert.Equal("Not found", result.Error);
    }

    private static DdCorrespondenceDetails ValidCorrespondenceDetails(string recipient)
    {
        return new DdCorrespondenceDetails
        {
            Body = "This is a test correspondence body.",
            Notification = new NotificationDetails
            {
                EmailBody = "This is a test email body.",
                EmailSubject = "Test Email Subject",
                EmailContentType = Dd.Correspondence.Models.EmailContentType.Html,
                SmsText = "Test SMS Text"
            },
            Recipient = recipient,
            Sender = "test-sender",
            SendersReference = "test-reference",
            ShipmentDatetime = DateTime.UtcNow,
            Summary = "Test Summary",
            Title = "Test Title",
            AllowForwarding = true,
            IdempotencyKey = Guid.NewGuid(),
            IgnoreReservation = false,
            VisibleDateTime = DateTime.UtcNow.AddHours(1),
        };
    }
}