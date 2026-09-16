namespace Altinn.Dd.Correspondence.Features.Get;

/// <summary>
/// Identifies the correspondence to retrieve an overview for.
/// </summary>
/// <param name="CorrespondenceId">The unique id of the correspondence.</param>
public record Request(Guid CorrespondenceId);
