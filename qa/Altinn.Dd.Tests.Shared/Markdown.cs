namespace Altinn.Dd.Tests.Shared;

/// <summary>Helpers for the markdown summaries both gates regenerate.</summary>
internal static class Markdown
{
    /// <summary>
    /// Escapes a value for a markdown table cell. Findings carry arbitrary text — a SonarQube rule
    /// message, a package id, a file path — and an unescaped pipe or newline silently breaks the
    /// table for every row after it.
    /// </summary>
    public static string Cell(string value)
        => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
