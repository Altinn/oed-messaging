
using Altinn.Dd.Correspondence.HttpClients;

namespace Altinn.Dd.Correspondence.Features.Search;

internal class Handler : IHandler<Query, Result>
{
    private readonly AltinnCorrespondenceClient _httpClient;

    public Handler(
        AltinnCorrespondenceClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result> Handle(Query query)
    {
        try
        {
            // Role field er obligatorisk, sjekk den
            if (query.Role == null)
            {
                return Result.Failure("Role is required for searching correspondences.");
            }

            if (query.ResourceId == null)
            {
                return Result.Failure("ResourceId is required for searching correspondences.");
            }
            // Denne returnerer en liste med guids
            // Sjekk hva den returnerer ordentlig
            var response = await _httpClient.CorrespondenceGETAsync(
                resourceId: query.ResourceId,
                from: query.From,
                to: query.To,
                status: (CorrespondenceStatusExt?)query.Status,
                role: (CorrespondencesRoleType?)query.Role,
                onBehalfOf: query.OnBehalfOf,
                sendersReference: query.SendersReference,
                idempotentKey: query.IdempotencyKey);

            return Result.Success(response.Ids);
        }
        catch (AltinnCorrespondenceException<ProblemDetails> e)
        {
            return Result.Failure(e.Result.Detail);
        }
    }
}
