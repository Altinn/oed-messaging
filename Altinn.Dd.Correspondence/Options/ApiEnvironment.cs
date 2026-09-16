namespace Altinn.Dd.Correspondence.Options;

/// <summary>
/// The Altinn 3 platform the client should call.
/// </summary>
public enum ApiEnvironment
{
    /// <summary>The live platform, <c>https://platform.altinn.no</c>.</summary>
    Production,

    /// <summary>The test platform TT02, <c>https://platform.tt02.altinn.no</c>.</summary>
    Staging,

    /// <summary>
    /// Local development. Altinn exposes no separate development endpoint, so this shares the
    /// test platform with <see cref="Staging"/>.
    /// </summary>
    Development
}
