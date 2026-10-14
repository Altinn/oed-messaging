namespace Altinn.Dd.Correspondence.Models;

/// <summary>
/// How the body of an email notification is interpreted.
/// </summary>
public enum EmailContentType
{
    /// <summary>Plain text.</summary>
    Plain = 0,

    /// <summary>HTML.</summary>
    Html = 1,
}
