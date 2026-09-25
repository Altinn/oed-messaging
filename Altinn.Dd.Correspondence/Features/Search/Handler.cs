
using Altinn.Dd.Correspondence.HttpClients;

namespace Altinn.Dd.Correspondence.Features.Search;

internal class Handler : IHandler<Query, Result<IEnumerable<Guid>>>
{
    private readonly AltinnCorrespondenceClient _httpClient;

    public Handler(
        AltinnCorrespondenceClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result<IEnumerable<Guid>>> Handle(Query query)
    {
        try
        {
            // Role field er obligatorisk, sjekk den
            if (query.Role == null)
            {
                return Result<IEnumerable<Guid>>.Failure("Role is required for searching correspondences.");
            }

            if (query.ResourceId == null)
            {
                return Result<IEnumerable<Guid>>.Failure("ResourceId is required for searching correspondences.");
            }
            // Denne returnerer en liste med guids
            // Sjekk hva den returnerer ordentlig
            var response = await _httpClient.CorrespondenceGETAsync(
                resourceId: query.ResourceId,
                // The generated client writes these without an offset and Altinn reads them as
                // UTC, so a local time would shift the window by the caller's offset.
                from: query.From?.ToUniversalTime(),
                to: query.To?.ToUniversalTime(),
                status: (CorrespondenceStatusExt?)query.Status,
                role: query.Role,
                onBehalfOf: query.OnBehalfOf,
                sendersReference: query.SendersReference,
                idempotentKey: query.IdempotencyKey);

            return Result<IEnumerable<Guid>>.Success(response.Ids);
        }
        catch (AltinnCorrespondenceException<ProblemDetails> e)
        {
            return Result<IEnumerable<Guid>>.Failure(e.Result.Detail);
        }
        catch (Polly.ExecutionRejectedException e)
        {
            throw Exceptions.ResilienceFailure.Translate(e);
        }
    }
}
