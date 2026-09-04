# Continuous integration and release

## `ci.yml` (push to `main`, pull requests)

| Job | What it proves |
|-----|----------------|
| build-test | `dotnet build` with warnings as errors, `dotnet format --verify-no-changes`, unit tests, golden regression tests |
| integration | the Redis adapters, the API and the worker against a `redis:7-alpine` service container (`REDIS_URL`), including the end-to-end and SignalR tests |
| images (matrix api / worker / mcp) | the Dockerfile builds each host; Trivy fails the job on unfixed HIGH / CRITICAL CVEs; layer cache in GitHub Actions |
| helm | `helm lint` for both value files; `helm template` piped through `kubeconform -strict` |

Tests run on Microsoft.Testing.Platform (`global.json`), so each project is invoked with `dotnet test --project`.
The integration job needs no Docker-in-Docker: when `REDIS_URL` is set the fixture skips Testcontainers.

## `release.yml` (tag `v*`)

- Images `ghcr.io/<owner>/coliseum-{api,worker,mcp}:<version>` with SBOM and provenance attestations.
- NuGet packages `Coliseum.Domain` and `Coliseum.Contracts` to GitHub Packages (the engine as a library for
  clients and balance tools, ADR-0011).
- Helm chart pushed as an OCI artifact to `ghcr.io/<owner>/charts/coliseum`.

Everything uses the repository's `GITHUB_TOKEN`; no long-lived secrets. Deploying to AWS would add an OIDC role
(see `deploy/terraform`) and an ECR login step.

## Running the same checks locally

```bash
make build test format          # dotnet
make docker-build helm-lint     # images and chart
```
