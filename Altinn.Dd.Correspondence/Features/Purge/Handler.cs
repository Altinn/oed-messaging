using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Features.Purge;

internal class Handler : IHandler<Request, CorrespondenceResult<Guid>>
{
    private readonly AltinnCorrespondenceClient _httpClient;

    public Handler(
        AltinnCorrespondenceClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CorrespondenceResult<Guid>> Handle(Request request)
    {
        try
        {
            var purgedId = await _httpClient.PurgeAsync(request.CorrespondenceId);
            return CorrespondenceResult<Guid>.Success(purgedId);
        }
        catch (AltinnCorrespondenceException<ProblemDetails> e)
        {
            return ApiRejection.ToFailure<Guid>(e);
        }
        catch (Polly.ExecutionRejectedException e)
        {
            throw Exceptions.ResilienceFailure.Translate(e);
        }
    }
}
