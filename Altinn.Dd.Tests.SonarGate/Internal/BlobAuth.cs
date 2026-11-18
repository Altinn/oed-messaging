using Azure.Identity;
using Azure.Storage.Blobs;

namespace Altinn.Dd.Tests.SonarGate.Internal;

/// <summary>
/// Shared factory for <see cref="BlobContainerClient"/> instances. Resolves auth in this order:
///   1. If <c>AZURE_STORAGE_SAS_TOKEN</c> is set, embed it in the container URI as a self-contained
///      SAS credential — no Azure SDK Credential object involved. Right path for environments that
///      can't issue federated AAD identities.
///   2. Otherwise fall back to <see cref="DefaultAzureCredential"/>: federated GitHub OIDC tokens
///      in CI, <c>az login</c> locally, managed identity, etc.
/// </summary>
internal static class BlobAuth
{
    public const string SasEnvVar = "AZURE_STORAGE_SAS_TOKEN";

    public static BlobContainerClient CreateContainerClient(string storageAccount, string containerName)
    {
        var sas = Environment.GetEnvironmentVariable(SasEnvVar);
        if (!string.IsNullOrWhiteSpace(sas))
        {
            // Tolerate "?sv=…" and "sv=…" — callers paste whichever the portal/CLI gave them.
            if (sas.StartsWith('?')) sas = sas[1..];
            var uri = new Uri($"https://{storageAccount}.blob.core.windows.net/{containerName}?{sas}");
            return new BlobContainerClient(uri);
        }

        var endpoint = new Uri($"https://{storageAccount}.blob.core.windows.net");
        return new BlobServiceClient(endpoint, new DefaultAzureCredential())
            .GetBlobContainerClient(containerName);
    }
}
