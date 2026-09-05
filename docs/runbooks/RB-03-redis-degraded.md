# RB-03: Redis degraded or unavailable

**Trigger:** `/healthz/ready` unhealthy on `redis` for API or worker, PING > 500 ms (degraded), timeouts in logs.

## Diagnose
1. `redis-cli INFO memory` — `used_memory` vs `maxmemory`; `evicted_keys` **must be 0** (policy is `noeviction`;
   any eviction would threaten settlement idempotency). If writes fail with OOM, the store is full: this is by design
   preferable to eviction.
2. `INFO persistence` — AOF rewrite in progress can add latency; `aof_last_write_status` must be `ok`.
3. `INFO clients` — connected clients: one multiplexer per host process is expected (2–3 per replica set).
4. `SLOWLOG GET 10` — settlement scripts should be well under 1 ms.
5. Network: NetworkPolicy allows only api/worker → redis; a new component needs the label `app.kubernetes.io/component` in `{api, worker}`.

## Mitigate
- Memory pressure: trim the battle stream is automatic (`MAXLEN ~ 1,000,000`); old `battle:{id}` hashes have no TTL
  (reports are permanent by design) — archive to object storage and delete if needed.
- Full outage: hosts keep running (`AbortOnConnectFail=false`) and reconnect; the API returns 500 for Redis-backed
  endpoints, the worker backs off. Nothing is lost: submitted battles are in the AOF-backed stream.
- Managed Redis (ElastiCache): fail over the replication group; hosts reconnect automatically.

## Verify
Readiness healthy on all hosts, PING latency in the health payload < 50 ms, pending drains.
