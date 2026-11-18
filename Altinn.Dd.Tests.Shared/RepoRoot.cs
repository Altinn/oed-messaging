namespace Altinn.Dd.Tests.Shared;

internal static class RepoRoot
{
    /// <summary>
    /// Walks up from the consumer's <c>[CallerFilePath]</c> until it finds a folder containing a
    /// <c>.sln</c> or <c>.slnx</c> file, treating that folder as the repo root for path resolution.
    /// </summary>
    public static string Resolve(string callerFilePath)
    {
        if (string.IsNullOrEmpty(callerFilePath))
            throw new InvalidOperationException(
                "[CallerFilePath] was not populated — SonarGate.RunAsync must be called from a method whose source-file path the compiler can capture.");

        var dir = new FileInfo(callerFilePath).Directory;
        while (dir is not null)
        {
            if (dir.EnumerateFiles().Any(f => f.Extension is ".sln" or ".slnx"))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate repo root (no .sln or .slnx above '{callerFilePath}').");
    }
}
