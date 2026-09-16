using System.Text.Json.Serialization;

namespace Altinn.Dd.InternalEvents.Estate;

/// <summary>
/// Payload of <see cref="EventType.DeclarationUnsigned"/>: a claim changed, so the heirs who had
/// already signed must sign the declaration again.
/// </summary>
public class DeclarationUnsignedEvent
{
    /// <summary>The party ids of the heirs whose signatures were invalidated.</summary>
    [JsonPropertyName("signeePartyIds")]
    public required IEnumerable<string> SigneePartyIds { get; set; }
}
