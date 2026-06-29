using System.Text.Json.Serialization;

namespace Altinn.Dd.InternalEvents.Estate;

public class DeclarationSubmittedV2Event
{
    [JsonPropertyName("daCaseId")]
    public required string DaCaseId { get; set; }
}