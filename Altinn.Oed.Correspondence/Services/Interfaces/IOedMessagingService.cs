using Altinn.Oed.Correspondence.ExternalServices.Correspondence;
using Altinn.Oed.Correspondence.Models;

namespace Altinn.Oed.Correspondence.Services.Interfaces;

/// <summary>
/// Represents a simple client used then communicating with the Altinn correspondence service.
/// </summary>
public interface IOedMessagingService
{
    /// <summary>
    /// Create a new correspondence element.
    /// </summary>
    /// <param name="correspondence">The correspondence subject.</param>
    /// <returns>A receipt indicating whether the correspondence was successfully created.</returns>
    Task<ReceiptExternal> SendMessage(OedMessageDetails correspondence);
}
