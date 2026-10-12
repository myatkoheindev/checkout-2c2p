namespace Checkout.Application.Common;

/// <summary>
/// The outcome of a service call: a value, or an error with a code. Business failures (an unknown
/// product, a reused idempotency key) are results, not exceptions; the API maps each
/// <see cref="ErrorCode"/> to an HTTP status and RFC 7807 problem details.
/// </summary>
/// <typeparam name="T">The value on success.</typeparam>
public sealed class Result<T>
{
    private Result(bool isSuccess, T? value, string? error, string? errorCode, IReadOnlyDictionary<string, string[]>? validationErrors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        ErrorCode = errorCode;
        ValidationErrors = validationErrors;
    }

    public bool IsSuccess { get; }

    public T? Value { get; }

    /// <summary>A human-readable reason, shown to the user.</summary>
    public string? Error { get; }

    /// <summary>One of <see cref="ErrorCodes"/>.</summary>
    public string? ErrorCode { get; }

    /// <summary>Field errors for <see cref="ErrorCodes.ValidationError"/>, keyed by field name.</summary>
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

#pragma warning disable CA1000 // Static factories on the generic type are the point of the pattern.
    public static Result<T> Success(T value) => new(true, value, null, null, null);

    public static Result<T> Failure(string error, string errorCode) => new(false, default, error, errorCode, null);

    public static Result<T> ValidationFailure(IReadOnlyDictionary<string, string[]> errors) =>
        new(false, default, "One or more validation errors occurred.", ErrorCodes.ValidationError, errors);
#pragma warning restore CA1000

    /// <summary>Transforms the value, carrying a failure through unchanged.</summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return IsSuccess
            ? Result<TNew>.Success(mapper(Value!))
            : ValidationErrors is not null
                ? Result<TNew>.ValidationFailure(ValidationErrors)
                : Result<TNew>.Failure(Error!, ErrorCode!);
    }
}
