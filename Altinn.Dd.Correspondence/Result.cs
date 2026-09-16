namespace Altinn.Dd.Correspondence;

/// <summary>
/// The outcome of a correspondence operation: either a value, or the detail of an API rejection.
/// </summary>
/// <remarks>
/// An API rejection that carries a problem document is reported here rather than thrown. Failures
/// that never reached Altinn - a timeout, an open circuit - throw
/// <see cref="Exceptions.CorrespondenceServiceException"/> instead, and any other status from the
/// API throws. See the package README for which statuses take which route.
/// </remarks>
/// <typeparam name="T">The value a successful operation produces.</typeparam>
public sealed class Result<T>
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the operation was rejected.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Gets the value produced, or <c>null</c> when the operation was rejected.</summary>
    public T? Value { get; }

    /// <summary>Gets the rejection detail reported by the API, or an empty string on success.</summary>
    public string Error { get; }

    private Result(
        bool isSuccess,
        T? value,
        string error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    /// <param name="value">The value the operation produced.</param>
    /// <returns>A successful result.</returns>
    public static Result<T> Success(T value) =>
        new(true, value, string.Empty);

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    /// <param name="error">The rejection detail reported by the API.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Failure(string error) =>
        new(false, default, error);

    /// <summary>
    /// Executes one of two functions based on the result state
    /// </summary>
    /// <typeparam name="TResult">The type both branches return.</typeparam>
    /// <param name="onSuccess">Invoked with the value when the operation succeeded.</param>
    /// <param name="onFailure">Invoked with the error detail when the operation was rejected.</param>
    /// <returns>Whatever the invoked branch returned.</returns>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        return IsSuccess
            ? onSuccess(Value!)
            : onFailure(Error);
    }
}
