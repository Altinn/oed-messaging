namespace Altinn.Dd.InternalEvents;

/// <summary>
/// A complete list of the internal events that are being published in the Digitalt Dødsbo system
/// </summary>
public static class EventType
{
    //public const string CaseStatusUpdated = "no.altinn.events.digitalt-dodsbo.v1.case-status-updated";
    public const string CaseStatusUpdateValidated = "no.altinn.events.digitalt-dodsbo.v1.case-status-update-validated";
    public const string DeclarationSubmitted = "no.altinn.events.digitalt-dodsbo.v1.declaration-submitted";
    public const string DeclarationUnsigned = "no.altinn.events.digitalt-dodsbo.v1.declaration-unsigned";
}