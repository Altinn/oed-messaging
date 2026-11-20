using Altinn.Dd.Tests.Shared;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Altinn.Dd.Tests.SonarGate.Internal;

/// <summary>
/// Keeps the SonarQube database between runs, so the quality gate has something to compare with.
///
/// Every run starts a fresh SonarQube container. Without an earlier analysis there is no new code,
/// and the default gate (Sonar way) only has new-code conditions, so a fresh container evaluates
/// nothing and every gate passes. Restoring the previous successful run's database gives SonarQube
/// that earlier analysis; the scanner's project version is the commit SHA, so "new code" means
/// everything since that run.
///
/// Blob layout, beside the run history under <c>{ProjectKey}/</c>:
/// <list type="bullet">
///   <item><c>sonar-db/{image}/sonar.mv.db.gz</c> — the database of the latest successful run.</item>
///   <item><c>sonar-db/{image}/backups/yyyyMMdd-HHmmss-{sha7}.mv.db.gz</c> — one per successful run,
///   so a damaged database can be replaced by an older one.</item>
/// </list>
/// Keyed by image because a database written by one SonarQube version needs a migration before
/// another version can open it, and an older version cannot open it at all. A new image therefore
/// starts from an empty database, and that one run compares with nothing.
///
/// Only a run whose gate passed is saved. Saving a failed run would make its new issues part of
/// the baseline, and the next run would pass without them being fixed.
///
/// The database holds the scanned source code, so the container these blobs live in must stay
/// private.
/// </summary>
internal sealed class SonarDatabaseStore
{
    /// <summary>Where the community image keeps its embedded H2 database.</summary>
    public const string ContainerDatabasePath = "/opt/sonarqube/data/sonar.mv.db";

    /// <summary>The image runs SonarQube as this user, which must be able to write the database.</summary>
    public const uint SonarQubeUserId = 1000;

    internal const string LatestName = "sonar.mv.db.gz";
    internal const string BackupFolder = "backups";
    internal const string CommitShaMetadata = "commitSha";

    /// <summary>
    /// Older backups tried after the latest copy, before falling back to an empty database. Each
    /// attempt costs a container start, so this stays small.
    /// </summary>
    internal const int BackupsToTry = 2;

    private readonly BlobContainerClient _container;
    private readonly string _prefix;
    private readonly string _workDir;
    private readonly Logger _logger;

    public SonarDatabaseStore(
        string projectKey,
        string sonarQubeImage,
        BlobContainerClient container,
        string workDir,
        Logger logger)
    {
        _prefix = Prefix(projectKey, sonarQubeImage);
        _container = container;
        _workDir = workDir;
        _logger = logger;
    }

    internal static string Prefix(string projectKey, string sonarQubeImage)
        => $"{projectKey}/sonar-db/{Regex.Replace(sonarQubeImage, "[^A-Za-z0-9._-]", "_")}";

    internal static string BackupName(string prefix, DateTime utc, string commitSha)
        => $"{prefix}/{BackupFolder}/"
           + $"{utc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{commitSha[..Math.Min(7, commitSha.Length)]}"
           + ".mv.db.gz";

    /// <summary>
    /// The databases to try, best first. Empty when there are none or the store can't be read, in
    /// which case the run starts from an empty database as it always did.
    /// </summary>
    public async Task<IReadOnlyList<string>> CandidatesAsync()
    {
        try
        {
            string? latestSha = null;
            var hasLatest = false;
            var backups = new List<(string Name, string? CommitSha)>();
            await foreach (var item in _container.GetBlobsAsync(
                               traits: BlobTraits.Metadata, prefix: $"{_prefix}/"))
            {
                item.Metadata.TryGetValue(CommitShaMetadata, out var sha);
                if (item.Name == $"{_prefix}/{LatestName}")
                {
                    hasLatest = true;
                    latestSha = sha;
                }
                else if (item.Name.StartsWith($"{_prefix}/{BackupFolder}/", StringComparison.Ordinal))
                {
                    backups.Add((item.Name, sha));
                }
            }
            return OrderCandidates(hasLatest ? $"{_prefix}/{LatestName}" : null, latestSha, backups);
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not list saved SonarQube databases (non-fatal): {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// The latest copy first, then the newest backups. A backup of the same run as the latest copy
    /// is the same database, so it is skipped: when the latest one is damaged, so is that backup.
    /// </summary>
    internal static IReadOnlyList<string> OrderCandidates(
        string? latestName,
        string? latestSha,
        IEnumerable<(string Name, string? CommitSha)> backups)
    {
        var older = backups
            .Where(b => latestName is null || latestSha is null || b.CommitSha != latestSha)
            .Select(b => b.Name)
            // Backup names start with a timestamp, so ordinal name order is chronological order.
            .OrderByDescending(n => n, StringComparer.Ordinal)
            .Take(BackupsToTry);
        return latestName is null ? [.. older] : [latestName, .. older];
    }

    /// <summary>Downloads and unpacks <paramref name="blobName"/>; returns the local database file.</summary>
    public async Task<string> DownloadAsync(string blobName)
    {
        Directory.CreateDirectory(_workDir);
        var file = Path.Combine(_workDir, "sonar.mv.db");
        await using var source = await _container.GetBlobClient(blobName).OpenReadAsync();
        await using var unzip = new GZipStream(source, CompressionMode.Decompress);
        await using var target = File.Create(file);
        await unzip.CopyToAsync(target);
        return file;
    }

    /// <summary>
    /// Saves the database of a successful run, as a new backup and as the latest copy. Failures are
    /// logged and never propagate: the next run then starts from an older copy, or an empty one.
    /// </summary>
    public async Task SaveAsync(byte[] database, string commitSha)
    {
        try
        {
            Directory.CreateDirectory(_workDir);
            var packed = Path.Combine(_workDir, LatestName);
            await using (var target = File.Create(packed))
            await using (var zip = new GZipStream(target, CompressionLevel.Optimal))
                await zip.WriteAsync(database);

            // Backup first: the latest copy must never be a database that has no backup.
            var backup = BackupName(_prefix, DateTime.UtcNow, commitSha);
            await UploadAsync(backup, packed, commitSha, overwrite: false);
            await UploadAsync($"{_prefix}/{LatestName}", packed, commitSha, overwrite: true);

            _logger.Log(
                $"Saved SonarQube database ({new FileInfo(packed).Length / (1024 * 1024)} MB packed) "
                + $"to blob {_container.Name}/{backup}.");
        }
        catch (Exception ex)
        {
            _logger.Log($"Saving the SonarQube database failed (non-fatal): {ex.Message}");
        }
    }

    private async Task UploadAsync(string blobName, string filePath, string commitSha, bool overwrite)
    {
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/gzip" },
            Metadata = new Dictionary<string, string> { [CommitShaMetadata] = commitSha },
            // If-None-Match: * — a backup is history and is never replaced.
            Conditions = overwrite ? null : new BlobRequestConditions { IfNoneMatch = ETag.All },
        };
        await _container.GetBlobClient(blobName).UploadAsync(filePath, options);
    }
}
