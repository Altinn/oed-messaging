namespace Altinn.Dd.Correspondence.Models;

/// <summary>
/// Represents the result of an operation that can either succeed or fail.
/// </summary>
public class CorrespondenceResult
{
    /// <summary>Gets a value indicating whether the correspondence was created.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the request was rejected.</summary>
    public bool IsFailure => !IsSuccess;
    
    /// <summary>Gets the receipt, or <c>null</c> when the request was rejected.</summary>
    public ReceiptExternal? Receipt { get; }
    
    /// <summary>Gets the rejection detail reported by the API, or an empty string on success.</summary>
    public string Error { get; }

    private CorrespondenceResult(
        bool isSuccess,
        ReceiptExternal? value, 
        string error)
    {
        IsSuccess = isSuccess;
        Receipt = value;
        Error = error;
    }

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    /// <param name="value">The receipt for the created correspondence.</param>
    /// <returns>A successful result.</returns>
    public static CorrespondenceResult Success(ReceiptExternal value) => 
        new(true, value, string.Empty);

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    /// <param name="error">The rejection detail reported by the API.</param>
    /// <returns>A failed result.</returns>
    public static CorrespondenceResult Failure(string error) => 
        new(false, default, error);

    /// <summary>
    /// Executes one of two functions based on the result state
    /// </summary>
    /// <typeparam name="TResult">The type both branches return.</typeparam>
    /// <param name="onSuccess">Invoked with the receipt when the correspondence was created.</param>
    /// <param name="onFailure">Invoked with the error detail when the request was rejected.</param>
    /// <returns>Whatever the invoked branch returned.</returns>
    public TResult Match<TResult>(
        Func<ReceiptExternal, TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        return IsSuccess 
            ? onSuccess(Receipt!) 
            : onFailure(Error);
    }
}