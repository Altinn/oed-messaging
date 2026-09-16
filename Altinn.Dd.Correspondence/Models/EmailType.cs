namespace Altinn.Dd.Correspondence.Models;

/// <summary>
/// How the body of an email notification should be interpreted.
/// </summary>
public enum EmailContentType
{
    /// <summary>The body is plain text.</summary>
    Plain = 0,

    /// <summary>The body is HTML.</summary>
    Html = 1
}
