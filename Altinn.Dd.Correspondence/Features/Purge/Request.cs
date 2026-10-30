namespace Altinn.Dd.Correspondence.Features.Purge;

/// <summary>
/// Identifies the correspondence to purge.
/// </summary>
/// <param name="CorrespondenceId">The unique id of the correspondence.</param>
public record Request(Guid CorrespondenceId);
