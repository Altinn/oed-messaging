using System.Text.Json.Serialization;

namespace Altinn.Dd.InternalEvents.Estate;

public class DeclarationUnsignedEvent
{
    [JsonPropertyName("signeePartyIds")]
    public required IEnumerable<string> SigneePartyIds { get; set; }
}