namespace Altinn.Dd.Correspondence.ExternalServices.Correspondence;

/// <summary>
/// Receipt response from Altinn correspondence service - compatible with Altinn 2 interface
/// This class provides backward compatibility for existing code that expects Altinn 2 style responses
/// </summary>
public class ReceiptExternal
{
    /// <summary>
    /// Gets or sets the receipt status code indicating success or failure
    /// </summary>
    public ReceiptStatusEnum ReceiptStatusCode { get; set; }

    /// <summary>
    /// Gets or sets the receipt text message providing additional details about the operation
    /// </summary>
    public string ReceiptText { get; set; } = string.Empty;

    /// <summary>
    /// Creates a successful receipt response
    /// </summary>
    /// <param name="message">Optional success message</param>
    /// <returns>A successful receipt</returns>
    public static ReceiptExternal CreateSuccess(string message = "Correspondence sent successfully")
    {
        return new ReceiptExternal
        {
            ReceiptStatusCode = ReceiptStatusEnum.OK,
            ReceiptText = message
        };
    }

    /// <summary>
    /// Creates an error receipt response
    /// </summary>
    /// <param name="message">Error message</param>
    /// <returns>An error receipt</returns>
    public static ReceiptExternal CreateError(string message)
    {
        return new ReceiptExternal
        {
            ReceiptStatusCode = ReceiptStatusEnum.Error,
            ReceiptText = message
        };
    }
}

/// <summary>
/// Receipt status enumeration - compatible with Altinn 2 interface
/// </summary>
public enum ReceiptStatusEnum
{
    /// <summary>
    /// Operation completed successfully
    /// </summary>
    OK,

    /// <summary>
    /// Operation failed
    /// </summary>
    Error
}
