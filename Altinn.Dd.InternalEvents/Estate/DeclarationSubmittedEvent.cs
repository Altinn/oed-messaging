using System.Text.Json.Serialization;

namespace Altinn.Dd.InternalEvents.Estate;

/// <summary>
/// Payload of <see cref="EventType.DeclarationSubmitted"/>: an heir submitted the declaration for
/// an estate.
/// </summary>
/// <remarks>
/// <see cref="DaCaseId"/> is a field rather than a property, and System.Text.Json skips public
/// fields under default options. This payload therefore serialises to <c>{}</c> and deserialises
/// <see cref="DaCaseId"/> to null unless you pass
/// <c>new JsonSerializerOptions { IncludeFields = true }</c>. Prefer
/// <see cref="DeclarationSubmittedV2Event"/>, which declares the same member as a property.
/// </remarks>
public class DeclarationSubmittedEvent
{
    /// <summary>The Domstoladministrasjonen case id of the estate.</summary>
    [JsonPropertyName("daCaseId")]
    public required string DaCaseId;
}
