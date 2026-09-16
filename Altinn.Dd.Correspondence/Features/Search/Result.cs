namespace Altinn.Dd.Correspondence.Features.Search;

/// <summary>
/// The outcome of searching for correspondences. An API rejection that carries a problem document is reported here
/// rather than thrown; see the package README for the statuses that still throw.
/// </summary>
public record Result
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the operation was rejected.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Gets the ids of the matching correspondences, or <c>null</c> when the operation was rejected.</summary>
    public IEnumerable<Guid>? Value { get; }

    /// <summary>Gets the rejection detail reported by the API, or an empty string on success.</summary>
    public string Error { get; }

    private Result(
        bool isSuccess,
        IEnumerable<Guid>? value,
        string error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    /// <param name="value">The ids of the matching correspondences.</param>
    /// <returns>A successful result.</returns>
    public static Result Success(IEnumerable<Guid> value) =>
        new(true, value, string.Empty);

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    /// <param name="error">The rejection detail reported by the API.</param>
    /// <returns>A failed result.</returns>
    public static Result Failure(string error) =>
        new(false, default, error);

    /// <summary>
    /// Executes one of two functions based on the result state
    /// </summary>
    /// <typeparam name="TResult">The type both branches return.</typeparam>
    /// <param name="onSuccess">Invoked with the value when the operation succeeded.</param>
    /// <param name="onFailure">Invoked with the error detail when the operation was rejected.</param>
    /// <returns>Whatever the invoked branch returned.</returns>
    public TResult Match<TResult>(
        Func<IEnumerable<Guid>, TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        return IsSuccess
            ? onSuccess(Value!)
            : onFailure(Error);
    }
}
