using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Features.Search;

public record Query(
    string? ResourceId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    CorrespondenceStatus? Status = null,
    CorrespondencesRoleType? Role = null,
    string? OnBehalfOf = null,
    string? SendersReference = null,
    Guid? IdempotencyKey = null);
