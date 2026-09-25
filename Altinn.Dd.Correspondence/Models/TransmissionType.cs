namespace Altinn.Dd.Correspondence.Models;

/// <summary>
/// The kind of transmission a correspondence becomes when it is sent to an existing Dialogporten
/// dialog. Names and values match Dialogporten's own transmission types.
/// </summary>
public enum TransmissionType
{
    /// <summary>General information, not related to any submission.</summary>
    Information = 1,

    /// <summary>Feedback or a receipt accepting a previous submission.</summary>
    Acceptance = 2,

    /// <summary>Feedback or an error message rejecting a previous submission.</summary>
    Rejection = 3,

    /// <summary>A question, or a request for more information.</summary>
    Request = 4,

    /// <summary>Critical information about the process.</summary>
    Alert = 5,

    /// <summary>Information about a formal decision.</summary>
    Decision = 6,

    /// <summary>A normal submission of some information or form.</summary>
    Submission = 7,

    /// <summary>A submission correcting or overriding previously submitted information.</summary>
    Correction = 8,
}
