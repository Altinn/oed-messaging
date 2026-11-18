namespace Altinn.Dd.Tests.SonarGate;

/// <summary>
/// Thrown by the SonarGate runner on quality-gate failures and unrecoverable scan errors. The
/// message carries the user-facing diagnostic (failing conditions, container/scan errors, etc.).
/// xUnit's test runner treats any uncaught exception as a test failure, so consumers don't need to
/// catch this — they let it bubble out of their <c>[SkippableFact]</c>.
/// </summary>
public sealed class SonarGateException : Exception
{
    public SonarGateException(string message) : base(message) { }
    public SonarGateException(string message, Exception inner) : base(message, inner) { }
}
