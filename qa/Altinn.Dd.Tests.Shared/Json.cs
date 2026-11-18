using System.Text.Json;

namespace Altinn.Dd.Tests.Shared;

/// <summary>
/// Tolerant accessors for <see cref="JsonElement"/>.
///
/// Both gates parse JSON they do not own — SonarQube's web API, the .NET SDK's report format — so
/// a missing or unexpectedly-typed property has to read as "absent" rather than throw. There were
/// three copies of these two methods before; this is the one.
/// </summary>
internal static class Json
{
    /// <summary>The string at <paramref name="name"/>, or empty if absent or not a string.</summary>
    public static string GetStringOrEmpty(JsonElement element, string name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    /// <summary>The int at <paramref name="name"/>, or null if absent or not a number.</summary>
    public static int? GetIntOrNull(JsonElement element, string name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32()
            : null;

    /// <summary>True when <paramref name="name"/> is present and is JSON <c>true</c>.</summary>
    public static bool GetBool(JsonElement element, string name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
