# ADR-0008: JWT HS256 with an API-key exchange, behind the IAuthTokenService port

- Status: Accepted
- Date: 2026-09-03

## Context and decision
"Protect all endpoints from unauthorized access", with no identity provider in the exercise. Every business
endpoint requires a bearer JWT (HS256, issuer/audience/lifetime validated). Service tokens (24 h) are obtained by
exchanging an API key at `POST /auth/token`; player tokens (1 h) are returned when a player is created. Token
issuing sits behind `IAuthTokenService`; validation is the host's JWT bearer middleware.

## Consequences
- Two roles (`service`, `player`) drive endpoint policies; data-dependent rules live in the use cases (ADR-0015).
- API keys are compared in constant time; the signing key and API keys are secrets injected through the
  environment / Kubernetes Secret / External Secrets, never committed (Development settings ship dev-only values).
- Swapping to a corporate IdP (Cognito, Auth0, Entra) means replacing `HmacJwtTokenService` and the bearer options
  with the IdP's JWKS; use cases and the MCP server do not change.
- Not covered: token revocation (short lifetimes mitigate), refresh tokens, per-player API keys.
