namespace Coliseum.Contracts.Errors;

/// <summary>
/// Error body shared by every endpoint, shaped like RFC 9457 Problem Details plus an <c>errors</c> list so a
/// client can highlight each offending field. Kept in Contracts (without depending on ASP.NET Core) so the
/// MCP server and the JavaScript client deserialize the same type the API produces.
/// </summary>
public sealed record ApiProblem(
    string Type,
    string Title,
    int Status,
    string? Detail,
    string? Instance,
    IReadOnlyList<ApiError> Errors);

/// <summary>One violated rule. <paramref name="Code"/> is stable; <paramref name="Message"/> is for humans.</summary>
public sealed record ApiError(string Code, string Message, string? Field);
