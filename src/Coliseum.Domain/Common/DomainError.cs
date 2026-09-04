namespace Coliseum.Domain.Common;

/// <summary>
/// Category of an expected failure. Hosts translate it into a transport status
/// (HTTP 400 / 403 / 404 / 409 / 500) without the domain knowing anything about HTTP.
/// </summary>
public enum DomainErrorKind
{
    /// <summary>Input broke a rule (length, range, format). Maps to 400.</summary>
    Validation,

    /// <summary>The referenced entity does not exist, or the caller is not allowed to know that it does. Maps to 404.</summary>
    NotFound,

    /// <summary>The operation collides with existing state, e.g. a duplicate name. Maps to 409.</summary>
    Conflict,

    /// <summary>The caller is authenticated but not allowed to do this. Maps to 403.</summary>
    Forbidden,

    /// <summary>An internal invariant or safety guard could not be honoured. Maps to 500 / dead-letter.</summary>
    Invariant,
}

/// <summary>
/// An expected failure expressed as data. It is never thrown; it travels inside <see cref="Result{T}"/>.
/// </summary>
/// <param name="Kind">Coarse category used for status mapping.</param>
/// <param name="Code">Stable, machine-readable identifier such as <c>player.name.too_long</c>. Clients switch on this, never on the message.</param>
/// <param name="Message">Human-readable explanation. Safe to show to an API consumer.</param>
/// <param name="Field">Name of the offending input when the error is about one specific field.</param>
public sealed record DomainError(DomainErrorKind Kind, string Code, string Message, string? Field = null)
{
    public static DomainError Validation(string field, string code, string message) =>
        new(DomainErrorKind.Validation, code, message, field);

    public static DomainError NotFound(string code, string message) =>
        new(DomainErrorKind.NotFound, code, message);

    public static DomainError Conflict(string code, string message, string? field = null) =>
        new(DomainErrorKind.Conflict, code, message, field);

    public static DomainError Forbidden(string code, string message) =>
        new(DomainErrorKind.Forbidden, code, message);

    public static DomainError Invariant(string code, string message) =>
        new(DomainErrorKind.Invariant, code, message);
}
