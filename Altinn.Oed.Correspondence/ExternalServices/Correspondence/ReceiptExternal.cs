namespace Altinn.Oed.Correspondence.ExternalServices.Correspondence;

/// <summary>
/// Receipt response from Altinn correspondence service - compatible with Altinn 2 interface
/// </summary>
public class ReceiptExternal
{
    /// <summary>
    /// Gets or sets the receipt status code
    /// </summary>
    public ReceiptStatusEnum ReceiptStatusCode { get; set; }

    /// <summary>
    /// Gets or sets the receipt text message
    /// </summary>
    public string ReceiptText { get; set; } = string.Empty;
}

/// <summary>
/// Receipt status enumeration - compatible with Altinn 2 interface
/// </summary>
public enum ReceiptStatusEnum
{
    OK,
    Error
}
