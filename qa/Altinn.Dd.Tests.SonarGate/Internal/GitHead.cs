using Altinn.Dd.Tests.Shared;
namespace Altinn.Dd.Tests.SonarGate.Internal;

/// <summary>
/// Resolves the current git HEAD commit SHA. Used by the unchanged-HEAD skip in
/// <see cref="SonarGateRunner"/>: a daily-cron run against the same commit would produce an
/// identical gate result, so there's no point paying for the scan.
/// </summary>
internal static class GitHead
{
    /// <summary>
    /// Runs <c>git rev-parse HEAD</c> in <paramref name="repoRoot"/>. Returns the trimmed SHA on
    /// success, or <c>null</c> on any failure (git not installed, not a repository, empty/detached)
    /// so a lookup failure never blocks a real QA run.
    /// </summary>
    public static async Task<string?> ResolveAsync(string repoRoot)
    {
        try
        {
            var (code, stdout, _) = await ProcessRunner.RunRawAsync("git", "rev-parse HEAD", repoRoot, null);
            if (code != 0) return null;
            var sha = stdout.Trim();
            return string.IsNullOrEmpty(sha) ? null : sha;
        }
        catch
        {
            return null;
        }
    }
}
