# Live events and the arena client

## Event flow

```
worker ──PUBLISH arena:events──▶ Redis ──▶ ArenaEventRelay (every API replica) ──▶ SignalR groups ──▶ browsers / back-office
```

| Event `type` | When | Recipients |
|--------------|------|------------|
| `battle.queued` | API accepted a battle | both players, back-office |
| `battle.turn` | after settlement, one per turn (battles ≤ 100 turns) | both players, back-office |
| `battle.done` | after settlement | both players, back-office |
| `battle.failed` | player missing / rules invariant | both players, back-office |
| `leaderboard.changed` | after every settlement (top 10) | everyone |

Events are published only after the settlement succeeded, so a client never animates a battle that did not count.
The payload is the JSON of `Coliseum.Contracts.Events.ArenaEvent` with a `type` discriminator (see `docs/api.md`).

## Hub `/hubs/arena`

- Authentication: the same JWT as the REST API, sent as `?access_token=` (browsers cannot set headers on WebSocket upgrades).
- On connect, a player token is placed in its own group `player:{id}`; nothing else is needed to receive its battles.
- Service tokens may call `JoinBackOffice()` (every event) or `WatchPlayer(playerId)`.
- Client method: `arenaEvent(json: string)`.

## Arena auto-play client (`/arena/`)

Open two windows:

```
http://localhost:8080/arena/?name=Ata&auto=1&interval=3000
http://localhost:8080/arena/?name=Bot&auto=1&interval=4000
```

Each window creates its player with the service API key (dev default `dev-service-key`), stores the player token in
`sessionStorage` (one identity per tab), connects to the hub, and with auto-play on picks a random opponent from
`GET /players` every `interval` ms. Turn events arrive in a burst (the engine takes microseconds); the client animates
them at 350 ms per turn with HP bars, then shows the loot and refreshes the leaderboard.

No build step: vanilla JavaScript plus `@microsoft/signalr` from cdnjs. Lives in `src/Coliseum.Api/wwwroot/arena` and is served by the API as static files.
