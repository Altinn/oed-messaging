using System.Text.Json.Serialization;

namespace Altinn.Dd.InternalEvents.Estate;

public class DeclarationSubmittedEvent
{
    [JsonPropertyName("daCaseId")]
    public required string DaCaseId;
}