using Altinn.Dd.Correspondence.Extensions;
using Altinn.Dd.Correspondence.HttpClients;

namespace Altinn.Dd.Correspondence.Features.Get;

internal class Handler : IHandler<Request, Result<CorrespondenceOverview>>
{
    private readonly AltinnCorrespondenceClient _httpClient;

    public Handler(
        AltinnCorrespondenceClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result<CorrespondenceOverview>> Handle(Request request)
    {
        try
        {
            var response = await _httpClient.CorrespondenceGET2Async(request.CorrespondenceId);
            return Result<CorrespondenceOverview>.Success(response.ToDto());
        }
        catch (AltinnCorrespondenceException<ProblemDetails> e)
        {
            return Result<CorrespondenceOverview>.Failure(e.Result.Detail);
        }
        catch (Polly.ExecutionRejectedException e)
        {
            throw Exceptions.ResilienceFailure.Translate(e);
        }
    }
}
