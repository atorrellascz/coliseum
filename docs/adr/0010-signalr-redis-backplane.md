# ADR-0010: Live events through Redis pub/sub relayed to SignalR (no backplane needed)

- Status: Accepted (implemented in MP-07; supersedes the original "SignalR with Redis backplane" wording)
- Date: 2026-09-05

## Context
Players and the back-office want to watch battles as they happen. The worker settles battles; the API holds the
client connections. With several API replicas, every replica must deliver the events of the players it serves.

## Decision
- The worker publishes `ArenaEvent`s (`battle.queued`, `battle.turn`, `battle.done`, `battle.failed`,
  `leaderboard.changed`) on the Redis channel `arena:events`, after the settlement succeeded.
- Every API replica runs `ArenaEventRelay`: it subscribes to the channel and forwards each event to the SignalR
  groups of its recipients (`player:{id}`), plus the `backoffice` group; leaderboard snapshots go to all.
- A connection is added to its own player group from the token on connect; `WatchPlayer` and `JoinBackOffice`
  require a service token. The raw JSON is forwarded, so the browser sees exactly the published contract.
- Turn events are published only for battles up to 100 turns; longer battles get the outcome only. Clients animate
  the burst (350 ms per turn in the arena client).
- No SignalR Redis backplane: server-initiated messages already fan out through the relay on every replica, and
  the hub has no server-to-server calls. The `Microsoft.AspNetCore.SignalR.StackExchangeRedis` package was removed.

## Consequences
- The worker never depends on SignalR; the API never depends on the worker. Both depend only on Redis.
- Adding replicas of the API needs no extra infrastructure; each subscribes on its own.
- If the hub ever needs server-to-server calls (kick a connection from another replica), the backplane can be added
  back with one line.
- Losing a pub/sub message (Redis pub/sub is fire-and-forget) loses a notification, never a battle: the report is
  always readable through `GET /battles/{id}`.
