namespace Coliseum.Contracts.Auth;

/// <summary>Bearer token issued by <c>POST /auth/token</c> (API key exchange) or alongside a new player.</summary>
/// <param name="AccessToken">JWT to send as <c>Authorization: Bearer</c>.</param>
/// <param name="ExpiresAt">Absolute expiry; clients should refresh before it.</param>
/// <param name="Role"><c>service</c> or <c>player</c>.</param>
public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt, string Role);
