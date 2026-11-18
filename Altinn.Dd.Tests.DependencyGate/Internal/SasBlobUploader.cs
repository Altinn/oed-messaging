using System.Net.Http.Headers;
using System.Text;
using Altinn.Dd.Tests.Shared;

namespace Altinn.Dd.Tests.DependencyGate.Internal;

/// <summary>
/// Uploads a snapshot to the QA blob container with a plain PUT against a container SAS URL.
///
/// SonarGate uses Azure.Storage.Blobs + Azure.Identity for this. Those two bring 12 transitive
/// packages, which is a poor trade for a single block-blob write in a package whose whole purpose
/// is to keep dependency risk visible and small. The workflows already supply
/// AZURE_STORAGE_SAS_TOKEN, so the SAS path is the one actually used in CI.
///
/// What is given up: the DefaultAzureCredential fallback, so a local run without the env var does
/// not archive. That is acceptable — archiving is non-fatal in both gates, and the local run still
/// writes the snapshot to disk.
/// </summary>
internal sealed class SasBlobUploader
{
    // Any recent stable service version; block-blob PUT semantics have been unchanged for years.
    private const string BlobApiVersion = "2021-12-02";

    public const string SasEnvVar = "AZURE_STORAGE_SAS_TOKEN";

    private readonly Logger _logger;
    private readonly string _storageAccount;
    private readonly string _container;

    public SasBlobUploader(string storageAccount, string container, Logger logger)
    {
        _storageAccount = storageAccount;
        _container = container;
        _logger = logger;
    }

    /// <summary>
    /// Uploads a file. Never throws: an archive failure must not turn a clean audit red, nor mask
    /// a dirty one. Returns whether the upload happened, for logging only.
    /// </summary>
    public async Task<bool> TryUploadAsync(string blobName, string filePath, string contentType)
    {
        var sas = Environment.GetEnvironmentVariable(SasEnvVar);
        if (string.IsNullOrWhiteSpace(sas))
        {
            _logger.Log($"{SasEnvVar} not set — skipping blob archive (snapshot is still on disk).");
            return false;
        }

        // Tolerate "?sv=…" and "sv=…" — callers paste whichever the portal or CLI gave them.
        if (sas.StartsWith('?')) sas = sas[1..];

        try
        {
            var uri = new Uri(
                $"https://{_storageAccount}.blob.core.windows.net/{_container}/{blobName}?{sas}");

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            using var content = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

            using var request = new HttpRequestMessage(HttpMethod.Put, uri) { Content = content };
            request.Headers.Add("x-ms-blob-type", "BlockBlob");
            request.Headers.Add("x-ms-version", BlobApiVersion);

            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.Log(
                    $"Blob archive failed (non-fatal): {(int)response.StatusCode} {response.ReasonPhrase}. "
                    + Summarise(body));
                return false;
            }

            _logger.Log($"Archived to blob {_container}/{blobName}.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log($"Blob archive failed (non-fatal): {ex.Message}");
            return false;
        }
    }

    // Azure returns an XML error document; keep the first line so a 403 from an expired SAS is
    // diagnosable without dumping the whole body into the test log.
    private static string Summarise(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";
        var firstLine = body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(l => l.Contains("Message", StringComparison.OrdinalIgnoreCase))
            ?? body;
        return firstLine.Length > 300
            ? new StringBuilder(firstLine, 0, 300, 300).Append('…').ToString()
            : firstLine.Trim();
    }
}
