namespace ObserwayLabelFlow.Core.Common;

public sealed class Result<T>
{
    private Result(bool isSuccess, T? value, IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public IReadOnlyList<string> Errors { get; }

    public static Result<T> Success(T value) => new(true, value, Array.Empty<string>());
    public static Result<T> Fail(params string[] errors) => new(false, default, errors);
    public static Result<T> Fail(IEnumerable<string> errors) => new(false, default, errors.ToArray());
}

