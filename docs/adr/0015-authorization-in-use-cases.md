# ADR-0015: Endpoint policies in the host, data-dependent authorization in the use cases

- Status: Accepted
- Date: 2026-09-04

## Context
"Protect all endpoints from unauthorized access" has two layers. Whether a caller may hit an endpoint at all
(only service tokens create players) is a static policy. Whether a caller may act on a *specific* record
(a player may only attack as themselves, may only read battles they took part in) depends on data the host
does not have.

## Decision
- The host maps the bearer token to a `Caller` (`Service` or `Player(id)`) and enforces static policies with
  ASP.NET Core authorization policies.
- Use cases receive the `Caller` and enforce the data-dependent rules themselves:
  `SubmitBattleHandler` forces the attacker to be the caller for player tokens; `GetBattleHandler` returns
  *not found* (never *forbidden*) to non-participants so the API does not reveal that a battle exists.
- Use cases never read claims, headers or HTTP types.

## Consequences
- Authorization rules that matter to the game are unit-tested with fakes, without an HTTP host.
- Swapping the token scheme (HS256 to a corporate IdP, ADR-0008) touches only the host and the
  `IAuthTokenService` adapter.
- The MCP server, which calls the API with a service token, inherits exactly the same rules.
