using Coliseum.Contracts.Errors;
using Coliseum.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Coliseum.Api.Middleware;

/// <summary>
/// Turns a failed <see cref="Result{T}"/> into an RFC 9457 Problem Details response with the full
/// <c>errors[{code, message, field}]</c> list, so a client can highlight every offending field at once.
/// Status comes from the error kind: validation 400, forbidden 403, not found 404, conflict 409, invariant 500.
/// </summary>
public static class ProblemDetailsMapping
{
    public static IResult ToResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : Problem(result.Errors);

    public static IResult Problem(IReadOnlyList<DomainError> errors)
    {
        var first = errors[0];
        (int status, string title) = first.Kind switch
        {
            DomainErrorKind.Validation => (StatusCodes.Status400BadRequest, "Validation failed"),
            DomainErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            DomainErrorKind.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            DomainErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Internal error"),
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = "urn:coliseum:problem:" + first.Kind.ToString().ToLowerInvariant(),
            Detail = first.Message,
        };
        problem.Extensions["errors"] = errors.Select(e => new ApiError(e.Code, e.Message, e.Field)).ToList();

        return Results.Problem(problem);
    }
}
