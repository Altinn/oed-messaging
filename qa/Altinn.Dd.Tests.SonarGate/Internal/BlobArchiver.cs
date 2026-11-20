using Altinn.Dd.Tests.Shared;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Altinn.Dd.Tests.SonarGate.Internal;

/// <summary>
/// Uploads the per-run snapshot JSON + regenerated <c>summary.md</c> into the QA blob container
/// under <c>{ProjectKey}/history/</c>. Pure archive — nothing here builds or deploys a dashboard.
///
/// The cross-project dashboard is rendered live by the admin apps, which read these same blobs
/// with a <c>Storage Blob Data Reader</c> grant on the storage account (see
/// <c>dd-infrastructure/infra/altinn-digdir-oed-tt02-rg/storage.tf</c>). There is no publish step
/// and no public endpoint: commit 919d140 removed this account's static website, so a blob written
/// here is reachable only through those role assignments or a short-lived read-only SAS.
///
/// Failures are caught and logged but never propagate — the gate result is the source of truth.
/// </summary>
internal sealed class BlobArchiver
{
    private readonly Logger _logger;
    private readonly string _projectKey;
    private readonly BlobContainerClient _container;
    private readonly string _localHistoryDir;

    public BlobArchiver(
        string projectKey,
        BlobContainerClient container,
        string localHistoryDir,
        Logger logger)
    {
        _projectKey = projectKey;
        _container = container;
        _localHistoryDir = localHistoryDir;
        _logger = logger;
    }

    public async Task ArchiveAsync(string snapshotFile)
    {
        try
        {
            var snapshotName = Path.GetFileName(snapshotFile);
            // Snapshots are history: never overwrite one. Names have one-second resolution, so two
            // runs of the same ProjectKey finishing in the same second would otherwise replace each
            // other silently. The second upload fails instead and is logged below. The name format
            // stays as it is because oed-admin reads this layout.
            await UploadFileAsync(
                $"{_projectKey}/history/{snapshotName}",
                snapshotFile,
                "application/json",
                overwrite: false);

            var summary = Path.Combine(_localHistoryDir, "summary.md");
            if (File.Exists(summary))
                await UploadFileAsync(
                    $"{_projectKey}/history/summary.md",
                    summary,
                    "text/markdown; charset=utf-8");

            _logger.Log($"Archived snapshot to blob {_container.Name}/{_projectKey}/history/{snapshotName}.");
        }
        catch (Exception ex)
        {
            _logger.Log($"Blob archive failed (non-fatal): {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the <c>commitSha</c> recorded by the most recent archived snapshot, or <c>null</c>
    /// when there is none yet, the newest snapshot predates the field (legacy), its quality gate
    /// was not <c>OK</c>, or the blob store can't be read. Callers treat <c>null</c> as "can't
    /// prove unchanged — run anyway", so a read failure here never causes a real run to be skipped,
    /// and a failed gate is reported again on every run until a new commit fixes it.
    /// </summary>
    public async Task<string?> GetLastArchivedCommitShaAsync()
    {
        try
        {
            // Snapshot blobs are named {ProjectKey}/history/yyyyMMdd-HHmmss.json, so the prefix is
            // identical across runs and ordinal name comparison == chronological order. The folder
            // also holds DependencyGate's deps-*.json, which sort after every timestamp and carry
            // no commitSha, so only names matching this gate's own pattern count.
            string? newest = null;
            await foreach (var item in _container.GetBlobsAsync(prefix: $"{_projectKey}/history/"))
            {
                if (!IsSonarSnapshot(item.Name)) continue;
                if (newest is null || string.CompareOrdinal(item.Name, newest) > 0)
                    newest = item.Name;
            }
            if (newest is null) return null;

            var content = await _container.GetBlobClient(newest).DownloadContentAsync();
            return CommitShaOfPassingRun(content.Value.Content.ToString());
        }
        catch (Exception ex)
        {
            _logger.Log($"Could not read last archived commit SHA (non-fatal): {ex.Message}");
            return null;
        }
    }

    /// <summary>True when the blob's basename is one of this gate's snapshots.</summary>
    internal static bool IsSonarSnapshot(string blobName)
        => HistoryWriter.SnapshotNamePattern.IsMatch(blobName[(blobName.LastIndexOf('/') + 1)..]);

    /// <summary>
    /// The snapshot's <c>commitSha</c> when its quality gate was <c>OK</c>, otherwise <c>null</c>.
    /// A failed run archives its snapshot before throwing; reusing that SHA would turn the next
    /// run at the same HEAD into a skip and hide the failure.
    /// </summary>
    internal static string? CommitShaOfPassingRun(string snapshotJson)
    {
        using var doc = JsonDocument.Parse(snapshotJson);
        var root = doc.RootElement;
        var passed = root.TryGetProperty("qualityGate", out var gate)
                     && Json.GetStringOrEmpty(gate, "status") == "OK";
        var sha = Json.GetStringOrEmpty(root, "commitSha");
        return passed && sha.Length > 0 ? sha : null;
    }

    private async Task UploadFileAsync(
        string blobName, string filePath, string contentType, bool overwrite = true)
    {
        var blob = _container.GetBlobClient(blobName);
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            // If-None-Match: * makes the service reject the upload when the blob already exists.
            Conditions = overwrite ? null : new BlobRequestConditions { IfNoneMatch = ETag.All },
        };
        await blob.UploadAsync(filePath, options);
    }
}
