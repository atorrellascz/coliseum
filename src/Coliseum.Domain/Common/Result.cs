using System.Diagnostics.CodeAnalysis;

namespace Coliseum.Domain.Common;

/// <summary>
/// Outcome of an operation that can fail for expected business reasons ("railway" style, PAT-07).
/// Exceptions are reserved for bugs and infrastructure faults; everything a caller is expected to handle
/// travels here as <see cref="DomainError"/> values. A failed result carries at least one error and may
/// carry several: validation reports every problem at once instead of stopping at the first.
/// </summary>
/// <typeparam name="T">Type of the value produced on success.</typeparam>
public sealed class Result<T>
{
    private static readonly IReadOnlyList<DomainError> NoErrors = [];

    private Result(T? value, IReadOnlyList<DomainError> errors)
    {
        Value = value;
        Errors = errors;
    }

    /// <summary>True when the operation produced a value. Guarantees <see cref="Value"/> is not null.</summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>True when at least one error was reported.</summary>
    [MemberNotNullWhen(false, nameof(Value))]
    public bool IsFailure => !IsSuccess;

    /// <summary>The produced value; only meaningful when <see cref="IsSuccess"/> is true.</summary>
    public T? Value { get; }

    /// <summary>Errors reported by the operation; empty on success.</summary>
    public IReadOnlyList<DomainError> Errors { get; }

    /// <summary>Kind of the first error, or null on success. Convenience for status mapping.</summary>
    public DomainErrorKind? ErrorKind => IsSuccess ? null : Errors[0].Kind;

    /// <summary>Transforms the value on success and forwards the errors on failure.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        IsSuccess ? Result<TOut>.Success(map(Value)) : Result<TOut>.Failure(Errors);

    /// <summary>Folds both branches into a single value.</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<IReadOnlyList<DomainError>, TOut> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Errors);

    // Factories are internal to keep static members off the generic type (CA1000); use the non-generic Result class.
    internal static Result<T> Success(T value) => new(value, NoErrors);

    internal static Result<T> Failure(IReadOnlyList<DomainError> errors) =>
        errors.Count == 0
            ? throw new ArgumentException("A failed result needs at least one error.", nameof(errors))
            : new(default, errors);
}

/// <summary>Entry points to build results without spelling out the generic argument at every call site.</summary>
public static class Result
{
    public static Result<T> Ok<T>(T value) => Result<T>.Success(value);

    public static Result<T> Fail<T>(DomainError error) => Result<T>.Failure([error]);

    public static Result<T> Fail<T>(IReadOnlyList<DomainError> errors) => Result<T>.Failure(errors);
}
