using Altinn.Oed.Messaging.ExternalServices.Correspondence;
using Altinn.Oed.Messaging.Models;

namespace Altinn.Oed.Messaging.Services.Interfaces;

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