using Altinn.Dd.Correspondence.Infrastructure;
using Altinn.Dd.Correspondence.Models;

namespace Altinn.Dd.Correspondence.Services.Interfaces;

/// <summary>
/// Represents a client for communicating with the Altinn 3 Correspondence service.
/// This interface maintains compatibility with the existing Altinn 2 messaging interface
/// to ensure seamless migration from Altinn 2 to Altinn 3.
/// </summary>
public interface IDdMessagingService
{
    /// <summary>
    /// Creates a new correspondence element in Altinn 3.
    /// </summary>
    /// <param name="correspondence">The correspondence details including recipient, content, and notifications.</param>
    /// <returns>A receipt indicating whether the correspondence was successfully created.</returns>
    /// <exception cref="CorrespondenceServiceException">Thrown when the correspondence creation fails.</exception>
    Task<ReceiptExternal> SendMessage(DdMessageDetails correspondence);
}
