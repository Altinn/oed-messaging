using Altinn.Dd.Correspondence.HttpClients;
using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Features.Search;

/// <summary>
/// Filters for a correspondence search. <paramref name="ResourceId"/> and <paramref name="Role"/>
/// are required by the Altinn 3 endpoint; a query missing either fails without calling the API.
/// The remaining fields are optional filters.
/// </summary>
/// <param name="ResourceId">The resource the correspondences were sent under. Required.</param>
/// <param name="From">Only return correspondences created at or after this point in time.</param>
/// <param name="To">Only return correspondences created at or before this point in time.</param>
/// <param name="Status">Only return correspondences currently in this status.</param>
/// <param name="Role">Whether to search as the sender, the recipient, or both. Required.</param>
/// <param name="OnBehalfOf">The party to search on behalf of, when acting for someone else.</param>
/// <param name="SendersReference">Only return correspondences carrying this senders reference.</param>
/// <param name="IdempotencyKey">Only return the correspondence created with this idempotency key.</param>
public record Query(
    string? ResourceId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    CorrespondenceStatus? Status = null,
    CorrespondencesRoleType? Role = null,
    string? OnBehalfOf = null,
    string? SendersReference = null,
    Guid? IdempotencyKey = null);
