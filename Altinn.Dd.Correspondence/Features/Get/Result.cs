namespace Altinn.Dd.Correspondence.Features.Get;

public record Result
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public CorrespondenceOverview? Value { get; }

    public string Error { get; }

    private Result(
        bool isSuccess,
        CorrespondenceOverview? value,
        string error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result Success(CorrespondenceOverview value) =>
        new(true, value, string.Empty);

    public static Result Failure(string error) =>
        new(false, default, error);

    /// <summary>
    /// Executes one of two functions based on the result state
    /// </summary>
    public TResult Match<TResult>(
        Func<CorrespondenceOverview, TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        return IsSuccess
            ? onSuccess(Value!)
            : onFailure(Error);
    }
}
