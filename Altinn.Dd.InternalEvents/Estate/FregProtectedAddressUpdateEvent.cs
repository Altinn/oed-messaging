using System.Text.Json.Serialization;

namespace Altinn.Dd.InternalEvents.Estate;

public class FregProtectedAddressUpdateEvent
{
    [JsonPropertyName("nin")]
    public required string Nin;
}