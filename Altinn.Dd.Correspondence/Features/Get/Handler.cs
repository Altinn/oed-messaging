using Altinn.Dd.Correspondence.Extensions;
using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Features.Get;

internal class Handler : IHandler<Request, CorrespondenceResult<CorrespondenceOverview>>
{
    private readonly AltinnCorrespondenceClient _httpClient;

    public Handler(
        AltinnCorrespondenceClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CorrespondenceResult<CorrespondenceOverview>> Handle(Request request)
    {
        try
        {
            var response = await _httpClient.CorrespondenceGET2Async(request.CorrespondenceId);
            return CorrespondenceResult<CorrespondenceOverview>.Success(response.ToDto());
        }
        catch (AltinnCorrespondenceException<ProblemDetails> e)
        {
            return CorrespondenceResult<CorrespondenceOverview>.Failure(e.Result.Detail);
        }
        catch (Polly.ExecutionRejectedException e)
        {
            throw Exceptions.ResilienceFailure.Translate(e);
        }
    }
}
