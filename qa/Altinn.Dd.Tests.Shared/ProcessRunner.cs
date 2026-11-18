using System.Diagnostics;
using System.Text;

namespace Altinn.Dd.Tests.Shared;

/// <summary>
/// Runs subprocesses for the QA gates.
///
/// Consolidates what used to be two near-identical implementations — SonarGate's ProcessRunner and
/// DependencyGate's DotnetCli — keeping the useful half of each: live streaming of stdout/stderr
/// through the logger (so a multi-minute scan shows progress instead of nothing-until-completion),
/// and a timeout that kills the process tree rather than hanging the test host.
///
/// Compiled into each gate from Altinn.Dd.Tests.Shared rather than shipped as its own package: nothing here
/// appears in either gate's public API, so a package would add a dependency to every consumer and
/// a public contract to maintain, for plumbing no consumer can call.
/// </summary>
internal sealed class ProcessRunner
{
    /// <summary>
    /// Long enough for a Sonar scan or a solution-wide restore, short enough that a wedged
    /// subprocess fails the run instead of occupying a runner until the job timeout.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    private readonly Logger _logger;
    private readonly Func<string, Exception> _onFailure;

    /// <param name="logger">Receives the subprocess output line by line as it arrives.</param>
    /// <param name="onFailure">
    /// Builds the exception thrown on a non-zero exit code. Supplied by the gate so the failure
    /// carries that gate's own exception type - this file is compiled into each gate separately
    /// and cannot reference either one.
    /// </param>
    public ProcessRunner(Logger logger, Func<string, Exception> onFailure)
    {
        _logger = logger;
        _onFailure = onFailure;
    }

    /// <summary>
    /// Runs a subprocess and throws on a non-zero exit code, with the full captured stdout/stderr
    /// in the message. Output is streamed through the logger as it arrives.
    /// </summary>
    public async Task RunAsync(
        string fileName,
        string args,
        string? workingDir = null,
        TimeSpan? timeout = null)
    {
        var (code, stdout, stderr) =
            await RunRawAsync(fileName, args, workingDir, _logger.StreamSubprocessLine, timeout);
        if (code != 0)
            throw _onFailure(
                $"`{fileName} {args}` exited with code {code}.{Environment.NewLine}"
                + $"STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}"
                + $"STDERR:{Environment.NewLine}{stderr}");
    }

    /// <summary>
    /// Runs a subprocess and returns the exit code plus captured streams without throwing. For
    /// callers that branch on the exit code — <c>git diff --cached --quiet</c>, or a
    /// <c>dotnet list</c> whose failure means "could not audit" rather than "audit failed".
    ///
    /// A timeout returns exit code -1 with the reason appended to stderr, so a caller that
    /// distinguishes failure modes sees it as a failed run rather than an exception.
    /// </summary>
    public static async Task<(int ExitCode, string Stdout, string Stderr)> RunRawAsync(
        string fileName,
        string args,
        string? workingDir = null,
        Action<string>? streamLog = null,
        TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo(fileName, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (workingDir is not null) psi.WorkingDirectory = workingDir;

        // A reused MSBuild node outlives the dotnet command that started it and inherits the
        // redirected stdout/stderr handles. WaitForExitAsync waits for those streams to close as
        // well as for the process to exit, so it then blocks until the timeout - which made every
        // DependencyGate run take ten minutes and then skip. Nothing here benefits from node reuse.
        psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stdout.AppendLine(e.Data);
            streamLog?.Invoke(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stderr.AppendLine(e.Data);
            streamLog?.Invoke(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var limit = timeout ?? DefaultTimeout;
        using var cts = new CancellationTokenSource(limit);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            return (-1, stdout.ToString(),
                $"{stderr}Timed out after {limit.TotalMinutes:0} minutes.");
        }

        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
