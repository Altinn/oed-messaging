using System.Text.Json.Serialization;

namespace Altinn.Dd.InternalEvents.Estate;

/// <summary>
/// Payload of <see cref="EventType.DeclarationSubmittedV2"/>: an heir submitted their declaration
/// for an estate under the one-form-per-heir approach. Subscribers must match on the full event
/// type string, since this payload is shaped like <see cref="DeclarationSubmittedEvent"/>.
/// </summary>
public class DeclarationSubmittedV2Event
{
    /// <summary>The Domstoladministrasjonen case id of the estate.</summary>
    [JsonPropertyName("daCaseId")]
    public required string DaCaseId { get; set; }
}
