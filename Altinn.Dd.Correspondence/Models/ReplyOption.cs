namespace Altinn.Dd.Correspondence.Models;

/// <summary>
/// A link the recipient can follow from the correspondence. Altinn shows each one as a button on the
/// correspondence's Dialogporten dialog.
/// </summary>
/// <param name="LinkUrl">An absolute https URL of at most 255 characters; Altinn rejects anything
/// else with error code 1035.</param>
/// <param name="LinkText">The button text. Without it Altinn shows "Gå til tjeneste".</param>
public record ReplyOption(string LinkUrl, string? LinkText = null);
