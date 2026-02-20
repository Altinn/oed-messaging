namespace Altinn.Dd.Correspondence.Features.Search;

public record Result
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public IEnumerable<Guid>? Value { get; }

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

    public static Result Success(IEnumerable<Guid> value) =>
        new(true, value, string.Empty);

    public static Result Failure(string error) =>
        new(false, default, error);

    /// <summary>
    /// Executes one of two functions based on the result state
    /// </summary>
    public TResult Match<TResult>(
        Func<IEnumerable<Guid>, TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        return IsSuccess
            ? onSuccess(Value!)
            : onFailure(Error);
    }
}
