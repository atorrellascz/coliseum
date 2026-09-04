# HTTP API

Interactive reference: `/scalar` (OpenAPI document at `/openapi/v1.json`). All business endpoints require
`Authorization: Bearer <token>`. Errors are RFC 9457 Problem Details with an `errors[{code, message, field}]` list.

## Authentication

```bash
API=http://localhost:8080
TOKEN=$(curl -s -X POST $API/auth/token -H "X-Api-Key: dev-service-key" | sed -E 's/.*"accessToken":"([^"]+)".*/\1/')
```

| Token | Obtained by | Lifetime | May |
|-------|-------------|----------|-----|
| service | `POST /auth/token` with `X-Api-Key` | 24 h | create players, submit battles for any attacker, read any battle |
| player | returned by `POST /players` | 1 h | submit battles as itself, read its own battles, read profiles and the leaderboard |

## Endpoints

### `POST /players` (service) → 201

```bash
curl -s -X POST $API/players -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Ata","description":"the first","gold":500,"silver":120,"attack":70,"defense":30,"hitPoints":100}'
```
Response: `{ "player": {...}, "accessToken": "<player token>", "expiresAt": "..." }`.
400 with every violated rule; 409 when the name is taken (case-insensitive).

### `GET /players/{id}` → 200 / 404

### `POST /battles` (player or service) → 202

```bash
curl -s -X POST $API/battles -H "Authorization: Bearer $PLAYER_TOKEN" -H "Content-Type: application/json" \
  -d '{"defenderId":"<opponent id>"}'
```
A service token must also send `attackerId`. A player token that sends someone else's `attackerId` gets 403.
Response: `{ "battleId": "...", "status": "queued", "submittedAt": "..." }` with `Location: /battles/{id}`.

### `GET /battles/{id}` → 200 / 404

While queued or processing only the header fields are present. When done:

```json
{
  "battleId": "01K...", "status": "done", "attackerId": "...", "defenderId": "...",
  "winnerId": "...", "loserId": "...", "turns": 7, "seed": 14121378471316891816,
  "loot": { "percent": 7, "gold": 35, "silver": 9, "score": 44 },
  "events": [ { "turn": 1, "attackerId": "...", "defenderId": "...", "attackerHpBefore": 100, "defenderHpBefore": 100,
                "attackValueUsed": 70, "dodgeChanceBasisPoints": 3000, "roll": 4943, "hit": true, "damage": 70, "defenderHpAfter": 30 } ],
  "narrative": [ "Ata challenges Bot. Seed 14121378471316891816.", "Turn 1: Ata hits Bot for 70 (attack 70). Bot has 30 HP left.", "..." ]
}
```
Non-participants receive 404, not 403.

### `GET /leaderboard?offset=0&limit=50` → 200

`{ "entries": [ { "rank": 1, "score": 44, "playerId": "..." } ], "offset": 0, "limit": 50, "total": 3 }`. `limit` ≤ 100.

## Operational endpoints (anonymous)

`GET /healthz/live`, `GET /healthz/ready`, `GET /metrics` (Prometheus text).

## Limits

- 100 requests per 10 s per token (or IP when anonymous): 429 with `Retry-After`.
- Request bodies up to 64 KB.
- Ids must match `[A-Za-z0-9_-]{1,64}`.
