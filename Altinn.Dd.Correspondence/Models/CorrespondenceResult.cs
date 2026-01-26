namespace Altinn.Dd.Correspondence.Models;

/// <summary>
/// Represents the result of an operation that can either succeed or fail.
/// </summary>
public class CorrespondenceResult
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;
    
    public ReceiptExternal? Receipt { get; }
    
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

    public static CorrespondenceResult Success(ReceiptExternal value) => 
        new(true, value, string.Empty);

    public static CorrespondenceResult Failure(string error) => 
        new(false, default, error);

    /// <summary>
    /// Executes one of two functions based on the result state
    /// </summary>
    public TResult Match<TResult>(
        Func<ReceiptExternal, TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        return IsSuccess 
            ? onSuccess(Receipt!) 
            : onFailure(Error);
    }
}