namespace Altinn.Dd.InternalEvents;

/// <summary>
/// A complete list of the internal events that are being published in the Digitalt Dødsbo system
/// </summary>
public static class EventType
{
    /// <summary>
    /// Event published by oed-events when ever a new update is received from DA via hendelsesliste
    /// </summary>
    public const string CaseStatusUpdateValidated = "no.altinn.events.digitalt-dodsbo.v1.case-status-update-validated";
    /// <summary>
    /// Event published by oed-events when a single case object is fetched and synced based on a manual trigger.
    /// </summary>
    public const string CaseStatusManuallySynced = "no.altinn.events.digitalt-dodsbo.v1.case-status-manually-synced";
    /// <summary>
    /// Event published by oed when a declaration is submitted for an estate by one of the heirs
    /// </summary>
    public const string DeclarationSubmitted = "no.altinn.events.digitalt-dodsbo.v1.declaration-submitted";
    /// <summary>
    /// Event published by oed when a declaration is submitted for an estate by an heir, 
    /// with the new one-form-per-heir approach. 
    /// </summary>
    public const string DeclarationSubmittedV2 = "no.altinn.events.digitalt-dodsbo.v2.declaration-submitted";
    /// <summary>
    /// Event published by oed-declaration when a claim has been changed requiring the other heirs to sign the declaration again
    /// </summary>
    public const string DeclarationUnsigned = "no.altinn.events.digitalt-dodsbo.v1.declaration-unsigned";
    /// <summary>
    /// Event published by oed-events when an update from Freg contains an individual with a protected address.
    /// </summary>
    public const string FregProtectedAddressUpdate = "no.altinn.events.digitalt-dodsbo.v1.freg-protected-address-update";
}