using Altinn.Dd.Correspondence.Extensions;
using Altinn.Dd.Correspondence.HttpClients;

namespace Altinn.Dd.Correspondence.Features.Get;

internal class Handler : IHandler<Request, Result>
{
    private readonly AltinnCorrespondenceClient _httpClient;

    public Handler(
        AltinnCorrespondenceClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result> Handle(Request request)
    {
        try
        {
            var response = await _httpClient.CorrespondenceGET2Async(request.CorrespondenceId);
            return Result.Success(response.ToDto());
        }
        catch (AltinnCorrespondenceException<ProblemDetails> e)
        {
            return Result.Failure(e.Result.Detail);
        }
    }
}
