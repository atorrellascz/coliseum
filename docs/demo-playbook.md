# Demo playbook

An ordered rehearsal script for the recording: every command, what it prints, what to look at, and what to say.
The demo runs on **Kubernetes only** (k3d + Helm + Argo CD); Compose is the alternative and is mentioned, not shown.
Commands are for **PowerShell** on Windows; the Bash form (Git Bash, Linux, macOS) is the same script without the
launcher, e.g. `bash scripts/k3d-up.sh` with `MODE=k8s KUBE_CONTEXT=k3d-coliseum bash scripts/chaos-worker.sh`.

## 0. Before you start

1. **A fresh PowerShell window.** `k3d` and `terraform` live in the user's `~/bin`, added to the user PATH; only
   windows opened afterwards see it. Check: `k3d version`, `terraform version`. Fallback: `$env:Path += ";$HOME\bin"`.
2. **Never a bare `bash`.** On a machine with WSL, `bash` in PowerShell is the Linux distro: no k3d, helm or
   kubectl, a different kubeconfig ("context k3d-coliseum does not exist") and no view of Windows ports
   ("Couldn't connect to localhost:8080"). Always `.\scripts\run.ps1 <script>.sh`, which runs Git Bash.
3. **Variables** are `$env:NAME = "value"` on their own line; `NAME=value command` is Bash syntax and PowerShell
   rejects it ("The term 'MODE=k8s' is not recognized").
4. `kubectl` may point at another cluster by default. **Every command here passes `--context k3d-coliseum`.**
5. Free ports: 8080 (API), 8082 (MCP), 3000 (Grafana), 8443 (Argo CD). `scripts/port-forward.sh` opens the first
   three (called by `k3d-up.sh` and `argocd-up.sh`); no manual port-forward is needed. Check with
   `netstat -ano | findstr :8080`; kill a stale process with `taskkill /F /PID <pid>`.
6. Clean up leftover jobs from earlier attempts: `Get-Job | Stop-Job; Get-Job | Remove-Job`.

## 1. Cluster + images + chart + smoke

```powershell
.\scripts\run.ps1 k3d-up.sh
```
Measured 2026-09-06: cluster 38 s; `docker save` + import ≈ 9 min the first time (4 GB, mostly otel-lgtm);
helm ≈ 4 min (Grafana/Prometheus/Loki/Tempo start-up). ≈ 3 min once the images are in the cluster.

Expected output, in order: `INFO[...] Creating node ...` (odd indentation = k3d's logger, not an error) →
`Cluster 'coliseum' created successfully!` → `cluster created in 38s` → `== building images` →
`== importing images` → `docker save (linux/amd64): ...` → `Successfully imported` with **no `ERRO` lines** →
`helm install took 225s` → **5 pods `Running`** → `== port-forward (API 8080, MCP 8082, Grafana 3000) + smoke` →
`SMOKE OK`.

Checks:
```powershell
kubectl --context k3d-coliseum -n coliseum get pods -o wide                 # 5 pods, 0 restarts, spread over the agents
kubectl --context k3d-coliseum -n coliseum get svc,hpa,pdb,networkpolicy    # api/mcp NodePort, API PDB, 2 NetworkPolicies
kubectl --context k3d-coliseum -n coliseum logs deploy/coliseum-coliseum-worker --tail 20
```
Worker logs (JSON, chronological): ~20 s of `Redis unavailable; backing off` and `Health check ... Unhealthy`
while Redis was starting (pods start in parallel; the worker **retries** instead of dying), then
`Battle processor started as consumer ... concurrency 4`, then one `Battle ... done` line per battle. What to say:
"the health-check errors at start-up are the readiness probe failing on purpose until Redis is up; the pod gets no
traffic and is not restarted".

What to say about the platform: "one chart, five workloads: API with HPA and PDB, worker `Recreate` with a 45 s
grace period, MCP, Redis with a PVC and `noeviction`, and for the demo an otel-lgtm with the dashboard provisioned;
NetworkPolicies: only api and worker reach Redis".

## 2. The game, live (two tabs)

- http://localhost:8080/arena/?name=Ata&auto=1 and http://localhost:8080/arena/?name=Bot&auto=1. Each tab creates its
  player with the API key `dev-service-key` (pre-filled), keeps its player token in `sessionStorage` and challenges a
  random opponent every 3 s: HP bars turn by turn, floating damage (crits in gold), misses, loot toasts, winner
  highlight, turn counter and the **seed** with the MCP replay hint. Keys: F fight, A auto, R refresh, S sound.
- Spectator: http://localhost:8080/arena/?watch=<Ata's id> → "Start watching" with the API key.
- Let them play ≥ 2 min before recording Grafana (rate panels use 1-5 min windows).

## 3. Back-office

http://localhost:8080/backoffice/ → API key `dev-service-key`.
- **RED tiles**: req/s, 5xx ratio, p95; **queue**: length / pending / DLQ; settled battles; attacker win rate vs the
  72 % from the simulation.
- **Charts**: req/s + p95, submitted vs settled per minute, turn buckets (need ~30 s of traffic).
- **Operations**: create a player and submit N battles between two players from the UI (same API, service token).
  "Recent players" below.
- **Live battles**: one row per battle (queued → processing → done); click → full report with narrative and seed. A row
  may briefly show `processing` with turns and a winner: turn events arrive before the `done` event.
- **Leaderboard live**.

## 4. Scalar test cases

http://localhost:8080/scalar. First `POST /auth/token` with header `X-Api-Key: dev-service-key` → copy `accessToken`
→ **Authorize** (Bearer). Then:

| # | Call | Expected |
|---|------|----------|
| 1 | `POST /players` valid body | 201, `Location`, the player and its 1-hour token |
| 2 | `POST /players` with `"name":""`, `"gold":-1`, `"attack":0` | 400 Problem Details with three `errors[]` |
| 3 | `POST /players` same name, upper case | 409 `player.name.taken` |
| 4 | `POST /battles` `{"attackerId":A,"defenderId":B}` | 202 + `Location` |
| 5 | `GET /battles/{id}` | `queued` → repeat → `done` with `narrative`, `seed`, `loot` |
| 6 | `GET /battles/{id}` with a third player's **player** token | 404 (existence not revealed) |
| 7 | `POST /battles` with a player token and someone else's `attackerId` | 403 `battle.attacker.mismatch` |
| 8 | `GET /leaderboard?limit=101` / `?limit=10` | 400 / 200 (rank, score, playerId) |
| 9 | `GET /admin/stats` with a player token / the service token | 403 / 200 |
| 10 | No token | 401. 101 requests in 10 s → 429 with `Retry-After` |

## 5. Grafana

http://localhost:3000 (admin / admin) → Dashboards → Coliseum → "Last 15 minutes", refresh 5 s.
- **API - RED**: req/s by route; 5xx ratio (flat at 0); p50/p95/p99 (p99 spikes are tabs starting up).
- **Queue and worker - USE**: submitted vs processed per minute (should overlap); submission→settlement p95;
  length / pending / DLQ / running. Stream length grows: acknowledged entries stay as a record until the automatic
  trim (`XADD MAXLEN ~ 1 000 000`).
- **Game economy**: resources stolen per minute; average turns (3-5).
- The **`duplicate`** series in "submitted vs processed" **exists only after a repeated delivery** (the chaos demo,
  step 8, causes one). In normal play it is not drawn.

## 6. MCP (an agent playing)

The MCP server is already at http://localhost:8082/mcp through `port-forward.sh`; no manual port-forward.
```powershell
.\scripts\run.ps1 mcp-demo.sh
```
Expected (verified 2026-09-06 against k3d): `1. initialize ... session: stateless` → `2. tools/list` with the 8 names →
`3. estimate_win_chance` `{"simulations":500,"attackerWinRate":0.66,...}` → `4. create_player x2` with two ids →
`5. play_battle` `status=Done winner=... turns=5 seed=...` plus the narrative → `6. get_leaderboard` → `MCP OK`.
Watch it land in the back-office feed and in "submitted vs processed".

**Postman**: `POST http://localhost:8082/mcp`, headers `X-Api-Key: dev-mcp-key`, `Content-Type: application/json`,
`Accept: application/json, text/event-stream`. Body 1 `initialize` (SSE answer `data: {...serverInfo...}`; the server
is stateless: no `Mcp-Session-Id` comes back and none is needed). Body 2 `notifications/initialized`. Then
`tools/list` and `tools/call` (exact JSON in `scripts/mcp-demo.sh`).
What to say: "the MCP server is one more client of the API: it inherits auth, validation and rate limits; only the
engine runs locally for the what-if".

## 7. GitOps with Argo CD

```powershell
.\scripts\run.ps1 argocd-up.sh
```
Installs Argo CD (`--server-side`: the ApplicationSet CRD exceeds the client-side annotation limit), uninstalls the
Helm release (Argo becomes the only owner of the namespace), applies `AppProject` + `Application` (source GitHub
`main`, `values-local.yaml`), waits for **`Synced/Healthy`**, **re-opens the 8080/8082/3000 port-forwards** (Argo
re-created the Services; reload the arena tabs) and prints the UI https://localhost:8443 with the `admin` password.
UI: application `coliseum` → resource tree (Redis, worker, api with HPA/PDB, mcp, NetworkPolicies, otel-lgtm),
sync waves 0→1→2→3.
Self-heal, live:
```powershell
kubectl --context k3d-coliseum -n coliseum delete deploy coliseum-coliseum-mcp
```
Argo recreates it when it notices the drift: within its refresh cycle (up to 3 min) or **immediately after Refresh
in the UI** (press Refresh for the recording).
What to say: "the cluster converges to the repo; nobody runs `kubectl apply` by hand; the API canary
(`api.rollout.enabled`) is promoted or aborted on Prometheus metrics".

## 8. Chaos: dead consumer → XAUTOCLAIM → exactly once

```powershell
$env:MODE = "k8s"
$env:KUBE_CONTEXT = "k3d-coliseum"
.\scripts\run.ps1 chaos-worker.sh
```
Pauses the real worker (scale to 0), queues 20 battles, a consumer named `ghost-crashed` reads them with `XREADGROUP`
and "dies" without `XACK`, shows `XPENDING` (20 under its name), resumes the worker and after ~30 s (`ClaimMinIdle`)
prints `Reclaimed 20 pending entries`, pending → 0 and **20/20 settled, not one more**. The arena tabs sit on `queued`
for the minute it takes, then catch up.
Quick variant without the script, while the tabs play:
`kubectl --context k3d-coliseum -n coliseum delete pod -l app.kubernetes.io/component=worker` → the new pod logs
`Reclaimed N pending entries`.
Where to see it: the script output, worker logs, the back-office queue tile (pending → 0), Grafana "pending" and,
after a repeated delivery, the `duplicate` series.

## 9. Terraform

```powershell
cd deploy\terraform
terraform init -backend=false
terraform validate          # "Success! The configuration is valid." (what CI runs)
terraform providers
cd ..\..
```
`terraform plan -var-file=envs/dev.tfvars` needs AWS credentials (the modules read availability zones and the caller
identity). Without an account show `validate`, `providers` and the README's cost table.

## 10. Teardown

```powershell
k3d cluster delete coliseum
Get-Job | Stop-Job; Get-Job | Remove-Job                             # leftover PowerShell jobs
Get-Process kubectl -ErrorAction SilentlyContinue | Stop-Process     # port-forwards
```

## Symptoms seen in rehearsal and their cause

| Symptom | Cause | Fix |
|---|---|---|
| `k3d is not installed` | `bash` = WSL, or a PowerShell window older than the PATH change | `.\scripts\run.ps1 ...` in a new window |
| `context "k3d-coliseum" does not exist` | `bash scripts/argocd-up.sh` ran in WSL (own kubeconfig) | `.\scripts\run.ps1 argocd-up.sh` |
| `Couldn't connect to localhost:8080` | script in WSL: Windows ports not visible | `.\scripts\run.ps1 chaos-worker.sh` with `$env:MODE="k8s"` |
| `The term 'MODE=k8s' is not recognized` | Bash syntax in PowerShell | `$env:MODE = "k8s"` on its own line |
| `terraform ... not recognized` | user PATH not loaded in that window | new window or `$env:Path += ";$HOME\bin"` |
| `ERRO ... content digest not found` during import | direct import with Docker's containerd image store | fixed: `scripts/k3d-import.sh` (tarball) |
| no `duplicate` series in Grafana | it exists only after a repeated delivery | cause one with step 8, or ignore |

## Alternative: Compose (mentioned, not shown)

"The same solution starts with Compose in one command; the demo uses Kubernetes." Never together with Kubernetes
(they share 8080/8082/3000). `docker compose -f deploy/compose/docker-compose.yml up --build -d`, `.\scripts\smoke.ps1`,
`down -v` to stop.
