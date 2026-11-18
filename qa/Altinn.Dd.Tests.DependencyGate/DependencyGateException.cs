namespace Altinn.Dd.Tests.DependencyGate;

/// <summary>
/// Thrown by the dependency gate when the audit finds advisories at or above the configured
/// severity threshold, or when the audit itself fails in a way that must not be mistaken for a
/// clean result. The message carries the user-facing diagnostic (the findings, or the reason the
/// audit could not complete). xUnit treats any uncaught exception as a test failure, so consumers
/// let this bubble out of their <c>[SkippableFact]</c>.
/// </summary>
public sealed class DependencyGateException : Exception
{
    public DependencyGateException(string message) : base(message) { }
    public DependencyGateException(string message, Exception inner) : base(message, inner) { }
}
