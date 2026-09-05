# Demo playbook

An ordered rehearsal script: every command, what it prints, what to look at, and what to say. Two tracks:
**Kubernetes** (the main track for a recording) and **Compose** (the 5-minute path). Commands are for Git Bash;
PowerShell equivalents are given where the syntax differs.

## 0. Before you start

- Docker Desktop running. Free ports: 8080-8082, 3000, 6379 (Compose); 18080, 13000, 8443 (Kubernetes port-forwards).
- Stale hosts: `netstat -ano | findstr :8080` → `taskkill /F /PID <pid>`.
- Compose and Kubernetes can run at the same time (the Kubernetes track only uses port-forwards), but a recording is
  cleaner with one of them. `docker compose ... down -v` frees Compose.
- `kubectl` on this machine points at a production cluster by default. **Every command below passes `--context`.**

## Track A: Kubernetes (k3d) + Argo CD

```bash
# 1. cluster + images + chart + smoke (≈ 3 min with warm images, ≈ 12 min the first time)
bash scripts/k3d-up.sh
#    -> "cluster created in Ns", "helm install took Ns", 5 pods Running (api, worker, mcp, redis, otel-lgtm), SMOKE OK
#    -> API http://localhost:18080, Grafana http://localhost:13000 (admin/admin)

# 2. the game, live
#    open http://localhost:18080/arena/?name=Ata&auto=1 and http://localhost:18080/arena/?name=Bot&auto=1
#    open http://localhost:18080/backoffice/  (API key dev-service-key): tiles, charts, live feed, operations panel

# 3. GitOps: hand the namespace to Argo CD
bash scripts/argocd-up.sh
#    -> Argo CD installed, AppProject + Application applied, "Synced/Healthy", UI https://localhost:8443 (admin / printed password)
#    -> in the UI: application "coliseum" -> resource tree: Redis StatefulSet, worker, api (+HPA/PDB), mcp, network policies
#    -> prove self-heal: kubectl --context k3d-coliseum -n coliseum delete deploy coliseum-coliseum-mcp ; watch Argo recreate it

# 4. chaos: a consumer takes 20 battles and dies without acknowledging; watch XAUTOCLAIM + exactly-once settlement
MODE=k8s KUBE_CONTEXT=k3d-coliseum API_URL=http://localhost:18080 bash scripts/chaos-worker.sh
#    -> XPENDING shows 20 entries owned by "ghost-crashed"; the worker logs "Reclaimed 20 pending entries"; settled 20/20, pending 0
#    (or simply: kubectl --context k3d-coliseum -n coliseum delete pod -l app.kubernetes.io/component=worker while the arena tabs play)

# 5. teardown
k3d cluster delete coliseum
```

## Track B: Compose (fast path)

```bash
docker compose -f deploy/compose/docker-compose.yml up --build -d      # first time ≈ 4-5 min (three images)
bash scripts/smoke.sh                                                   # Git Bash
.\scripts\smoke.ps1                                                     # PowerShell
```

| Open | What to show |
|------|--------------|
| http://localhost:8080/arena/?name=Ata&auto=1 and `?name=Bot&auto=1` | two tabs fighting: HP bars, floating damage, loot toasts, seed line, leaderboard |
| http://localhost:8080/backoffice/ (key `dev-service-key`) | RED tiles and charts (need ~30 s of traffic), queue USE, economy, live feed, **Operations**: create players and submit battles from the UI, click a battle for the report |
| http://localhost:8080/widget/ | paste a player token (arena tab → DevTools → `sessionStorage.getItem('coliseum.player')`), mount the widget |
| http://localhost:3000 → Coliseum | Grafana: rate panels fill after ~1 min of traffic; stat panels immediately |
| http://localhost:8080/scalar | API reference; test cases in §Scalar below |

Scripted demos:

```bash
bash scripts/mcp-demo.sh        # MCP: initialize, tools/list, estimate_win_chance, create_player x2, play_battle, leaderboard
bash scripts/chaos-worker.sh    # a dead consumer leaves 20 entries pending; watch XAUTOCLAIM reclaim them and settle exactly once
```

## Scalar test cases (with the token in "Authorize")

1. `POST /auth/token` with header `X-Api-Key: dev-service-key` → 200, copy `accessToken`, paste into Authorize (Bearer).
2. `POST /players` valid body → 201 (note `Location` and the player `accessToken`).
3. `POST /players` with `"name": ""`, `"gold": -1`, `"attack": 0` → 400 Problem Details with `errors[]` (three fields).
4. `POST /players` same name again → 409 `player.name.taken`.
5. `POST /battles` `{"attackerId": A, "defenderId": B}` → 202 with `Location`.
6. `GET /battles/{id}` → 200 `queued` → repeat → `done` with `narrative`, `seed`, `loot`.
7. `GET /battles/{id}` with a **player** token of a third player → 404 (existence not revealed).
8. `POST /battles` with a player token and someone else's `attackerId` → 403 `battle.attacker.mismatch`.
9. `GET /leaderboard?limit=101` → 400; `?limit=10` → 200 rank/score/playerId.
10. `GET /admin/stats` with a player token → 403; with the service token → 200.
11. Remove the token → any endpoint → 401. Send 101 requests in 10 s → 429 with `Retry-After`.

## MCP with Postman

`POST http://localhost:8082/mcp`, headers `X-Api-Key: dev-mcp-key`, `Content-Type: application/json`,
`Accept: application/json, text/event-stream`. Body 1: `initialize` (the answer is an SSE frame `data: {...serverInfo...}`;
this server runs stateless, so no `Mcp-Session-Id` header comes back and none is needed on later calls; a stateful
server would return one to echo back). Body 2: `notifications/initialized`. Then `tools/list`, and `tools/call` with
`estimate_win_chance`, `create_player`, `play_battle`, `get_leaderboard` (exact JSON in `scripts/mcp-demo.sh`).
What to watch: the back-office live feed and leaderboard update as the agent plays; Grafana "submitted vs processed".

## Where recovery is visible (worker kill)

| Signal | Where |
|--------|-------|
| entries delivered but not acknowledged | `redis-cli XPENDING coliseum:battles:stream workers` (Compose: `docker compose exec redis redis-cli ...`) |
| reclaim by the new worker | worker logs: `Reclaimed N pending entries from idle consumers` (≈ 30 s after start, `ClaimMinIdle`) |
| a battle settled before the kill and re-delivered | `coliseum_battles_processed_total{result="duplicate"}` (Grafana "processed duplicate"), balances unchanged |
| overall | back-office queue tile (length / pending / DLQ) and Grafana "Stream length / pending / DLQ" |

## Terraform

```bash
cd deploy/terraform
terraform init -backend=false && terraform validate     # no credentials needed (what CI runs)
# with AWS credentials (AWS_PROFILE or AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY):
terraform init && terraform plan -var-file=envs/dev.tfvars   # ≈ 90 resources planned, nothing applied
```

`plan` needs credentials because the VPC module reads availability zones and the EKS module reads the caller
identity. Without an AWS account, show `validate`, `terraform providers` and the README's cost table.
