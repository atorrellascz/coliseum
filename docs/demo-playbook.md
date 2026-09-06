# Demo playbook

An ordered rehearsal script: every command, what it prints, what to look at, and what to say. Two tracks:
**Kubernetes** (the main track for a recording) and **Compose** (the 5-minute path). Commands are for Git Bash;
PowerShell equivalents are given where the syntax differs.

## 0. Before you start

- Docker Desktop running. Free ports: 8080, 8082, 3000 (both tracks), 8081 and 6379 (Compose), 8443 (Argo CD UI).
- **Where to run the scripts** (`scripts/*.sh` are Bash; every one passes `--context` to kubectl):

  | Environment | Command | Notes |
  |---|---|---|
  | Windows, PowerShell (the recording) | `.\scripts\run.ps1 k3d-up.sh` | `run.ps1` locates Git Bash and runs the script with it. A plain `bash` in PowerShell is WSL when WSL is installed, and WSL has no k3d, helm or kubectl. Variables: `$env:MODE="k8s"; $env:KUBE_CONTEXT="k3d-coliseum"; .\scripts\run.ps1 chaos-worker.sh`. `smoke.ps1` goes through the launcher. |
  | Windows, Git Bash | `bash scripts/k3d-up.sh` | As is (Git Bash puts `~/bin`, where `k3d.exe` lives, on the PATH). Bash-style variables: `MODE=k8s KUBE_CONTEXT=k3d-coliseum bash scripts/chaos-worker.sh`. |
  | Linux / macOS | `bash scripts/k3d-up.sh` | Needs `docker`, `kubectl`, `helm`, `k3d` and `curl` on the PATH. Nothing else differs. |
  | WSL (Ubuntu) | `bash scripts/k3d-up.sh` inside the distro | Only after installing `kubectl`, `helm` and `k3d` in the distro and enabling Docker Desktop's WSL integration for it. WSL has its own kubeconfig. Ports 8080/8082/3000 opened from WSL are reachable from a Windows browser. Not recommended for the recording: a second environment to keep in shape. |

  Expected `k3d-up.sh` output: `INFO[00xx] Creating node ...`, `Cluster 'coliseum' created successfully!` (the odd
  indentation of those lines is k3d's logger using carriage returns, not an error), `cluster created in ~40 s`,
  `== building images`, `== importing images` (a `docker save` of ~4 GB, then `Successfully imported`; no `ERRO` lines),
  `helm install took Ns`, 5 pods Running, `SMOKE OK` and the URLs.
- Stale hosts: `netstat -ano | findstr :8080` → `taskkill /F /PID <pid>`.
- **One stack at a time.** The Kubernetes port-forwards (`scripts/port-forward.sh`) use the same local ports as
  Compose (API 8080, MCP 8082, Grafana 3000) so every URL below is identical on both tracks; the script refuses to
  start while Compose is up, and Compose fails to bind 8080 while the forwards are up. Switch with
  `docker compose -f deploy/compose/docker-compose.yml down -v` or `pkill -f port-forward`.
- `kubectl` on this machine points at a production cluster by default. **Every command below passes `--context`.**

## Track A: Kubernetes (k3d) + Argo CD

```bash
# 1. cluster + images + chart + smoke (≈ 3 min with warm images, ≈ 12 min the first time)
bash scripts/k3d-up.sh
#    -> "cluster created in Ns", "helm install took Ns", 5 pods Running (api, worker, mcp, redis, otel-lgtm), SMOKE OK
#    -> API http://localhost:8080, MCP http://localhost:8082/mcp, Grafana http://localhost:3000 (admin/admin)

# 2. the game, live
#    open http://localhost:8080/arena/?name=Ata&auto=1 and http://localhost:8080/arena/?name=Bot&auto=1
#    open http://localhost:8080/backoffice/  (API key dev-service-key): tiles, charts, live feed, operations panel

# 3. GitOps: hand the namespace to Argo CD
bash scripts/argocd-up.sh
#    -> Argo CD installed, AppProject + Application applied, "Synced/Healthy", UI https://localhost:8443 (admin / printed password)
#    -> port-forwards 8080/8082/3000 re-opened (Argo re-created the Services); the arena tabs just need a reload
#    -> in the UI: application "coliseum" -> resource tree: Redis StatefulSet, worker, api (+HPA/PDB), mcp, network policies
#    -> prove self-heal: kubectl --context k3d-coliseum -n coliseum delete deploy coliseum-coliseum-mcp ; Argo recreates it
#       within its refresh interval (up to 3 min) or immediately after you press Refresh in the UI

# 4. chaos: a consumer takes 20 battles and dies without acknowledging; watch XAUTOCLAIM + exactly-once settlement
MODE=k8s KUBE_CONTEXT=k3d-coliseum bash scripts/chaos-worker.sh
#    -> XPENDING shows 20 entries owned by "ghost-crashed"; the worker logs "Reclaimed 20 pending entries"; settled 20/20, pending 0
#    (or simply: kubectl --context k3d-coliseum -n coliseum delete pod -l app.kubernetes.io/component=worker while the arena tabs play)

# 5. teardown (also drops the port-forwards)
k3d cluster delete coliseum; pkill -f port-forward
```

## Track B: Compose (fast path)

Same URLs as track A (stop the Kubernetes port-forwards first: `pkill -f port-forward`).

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
