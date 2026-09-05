# Security

Threat model and controls for the exercise. The system handles no real money and no personal data beyond a display
name, so the goal is: no unauthenticated access, no cross-player actions, no trivial abuse, hardened containers.

## Authentication and authorization

| Control | Where |
|---------|-------|
| Every business endpoint and the SignalR hub require a bearer JWT (HS256, issuer, audience, lifetime, 30 s skew); a fallback policy makes anonymous the exception, granted explicitly to `/healthz/*`, `/metrics`, OpenAPI and static files | `Api/Auth/AuthPolicies.cs`, `Api/HostingExtensions.cs` |
| API key → service token exchange with constant-time comparison; keys come from configuration secrets | `Api/Auth/ApiKeyExchange.cs`, `Application/Options/AuthOptions.cs` |
| Service tokens live 24 h, player tokens 1 h | `Api/Auth/HmacJwtTokenService.cs` |
| Endpoint policies: only service tokens create players; players and services submit battles and read | `Api/Endpoints/*` |
| Data-dependent rules in use cases: a player attacks only as itself (403), reads only battles it took part in (404, existence not revealed) | `SubmitBattleHandler`, `GetBattleHandler` (ADR-0015) |
| Hub connections join their own player group from the token; watching others requires a service token | `Api/Hubs/ArenaHub.cs` |
| MCP HTTP transport requires its own client key; the MCP server holds a service token and forwards the API's rules | `Mcp/McpApiKeyGuard.cs`, ADR-0013 |

## Input handling

- Ids are validated against `[A-Za-z0-9_-]{1,64}` before any key is built (`Domain/Common/Identifier.cs`); Redis
  keys are assembled in one place (`RedisKeys`).
- Player input is validated by the aggregate with every violation reported; limits from the spec (20-char names,
  1,000-char descriptions, 1e9 resources) plus stat caps (SUP-11).
- Request bodies are limited to 64 KB; JSON binding is strict about types.
- Rate limiting: 100 requests / 10 s per token (hashed, never stored raw) or per IP; `429` with `Retry-After`;
  probes and scrapers exempt (`Api/Options/RateLimitOptions.cs`).
- Conservative response headers (`nosniff`, `DENY` framing, no referrer, no store), explicit CORS allow-list.

## Secrets

- Signing key, API keys and the MCP client key are configuration values read from environment variables,
  Kubernetes Secrets (`secrets.existingSecret` for External Secrets Operator) or Compose variables.
  `appsettings.Development.json` ships dev-only values and says so.
- No secret is logged; tokens are hashed before use as rate-limit partition keys.

## Containers and cluster

- Images: chiseled Ubuntu (no shell, no package manager), non-root user, read-only root filesystem, `/tmp` on
  tmpfs, `no-new-privileges`, all capabilities dropped, seccomp `RuntimeDefault` (Dockerfile, Compose, Helm).
- Trivy gates CI on unfixed HIGH/CRITICAL CVEs; images carry SBOM and provenance attestations on release.
- NetworkPolicies: only api and worker may reach Redis; hosts accept only port 8080.
- Redis is not exposed outside the cluster; in Compose it is published on localhost for development only.
- ServiceAccount tokens are not mounted (`automountServiceAccountToken: false`).

## Not covered (deliberately)

- Token revocation / refresh (short lifetimes mitigate), per-player API keys, MFA: an IdP would provide them
  (ADR-0008).
- Redis AUTH/TLS in Compose and the embedded chart Redis; ElastiCache in Terraform enables both.
- Anti-cheat: the battle seed is public by design (ADR-0004); stats are supplied by the creator, not earned.
- DDoS beyond per-caller rate limits (an ingress/WAF concern).
- Audit log: battle reports are immutable records, but player creation and token issuance are only in logs.
