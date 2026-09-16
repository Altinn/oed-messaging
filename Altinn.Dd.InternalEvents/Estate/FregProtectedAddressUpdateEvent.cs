using System.Text.Json.Serialization;

namespace Altinn.Dd.InternalEvents.Estate;

/// <summary>
/// Payload of <see cref="EventType.FregProtectedAddressUpdate"/>: an update from Freg concerned an
/// individual with a protected address.
/// </summary>
public class FregProtectedAddressUpdateEvent
{
    /// <summary>The national identity number of the individual with the protected address.</summary>
    [JsonPropertyName("nin")]
    public required string Nin { get; set; }
}
