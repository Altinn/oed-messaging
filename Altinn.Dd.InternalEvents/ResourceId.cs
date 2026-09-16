namespace Altinn.Dd.InternalEvents;

/// <summary>
/// A list of the resource IDs for the internal events that are being published in the Digitalt dødsbo system
/// </summary>
public static class ResourceId
{
    /// <summary>The dd-private-probate Altinn app.</summary>
    public const string DdPrivateProbate = "urn:altinn:resource:app_digdir_dd-private-probate";

    /// <summary>The oed-declaration Altinn app.</summary>
    public const string OedDeclaration = "urn:altinn:resource:app_digdir_oed-declaration";

    /// <summary>The Domstoladministrasjonen probate API.</summary>
    public const string DomstolApi = "urn:altinn:resource:dodsbo-domstoladmin-api";
}
