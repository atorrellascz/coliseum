# Terraform: Coliseum on AWS

Infrastructure as code for a production-shaped environment. **Validated, not applied**: `terraform validate`
runs locally and in CI; nobody paid for an EKS cluster to grade a hands-on test.

## What it creates

| Module / resource | Purpose |
|-------------------|---------|
| `vpc` (terraform-aws-modules/vpc) | 3 AZs, 3 private + 3 public subnets, NAT (single in non-prod), subnet tags for load balancers |
| `eks` (terraform-aws-modules/eks) | EKS 1.31, IRSA enabled, one managed node group with Cluster Autoscaler tags, core add-ons |
| `ecr` ×3 | `coliseum-api`, `coliseum-worker`, `coliseum-mcp`: immutable tags, scan on push, keep the last 30 images |
| `elasticache` (terraform-aws-modules/elasticache) | Redis 7.1 replication group, 2 nodes multi-AZ with automatic failover, TLS + auth token, parameter group with **`maxmemory-policy = noeviction`** (SUP-10), reachable only from the EKS node security group |
| `aws_secretsmanager_secret.app` | One JSON secret: `signingKey`, `apiKey`, `mcpClientKey`, `redisUrl` (StackExchange.Redis syntax with TLS and the auth token) |
| `external_secrets_irsa` | IAM role for the External Secrets Operator via IRSA, allowed to read only that secret |

## How it connects to the Helm chart

`terraform output helm_values_snippet` prints the values to pass to `deploy/helm/coliseum`: ECR registry,
external Redis, `secrets.existingSecret`. An `ExternalSecret` (namespace `coliseum`) materialises the AWS secret as
the Kubernetes Secret the chart expects; the operator's ServiceAccount is annotated with `external_secrets_role_arn`.

## Commands

```bash
cd deploy/terraform
terraform fmt -check -recursive
terraform init -backend=false        # downloads providers and modules, no credentials needed
terraform validate

# with credentials and a state backend (see backend.tf):
terraform init
terraform plan  -var-file=envs/dev.tfvars
terraform apply -var-file=envs/dev.tfvars
```

## Cost estimate (dev, eu-west-1, on-demand, September 2026 list prices, approximate)

| Item | ≈ USD / hour | ≈ USD / month |
|------|-------------|---------------|
| EKS control plane | 0.10 | 73 |
| 2 × t3.medium nodes | 0.09 | 67 |
| 1 NAT gateway (+ data) | 0.05 | 35 + traffic |
| ElastiCache 2 × cache.t4g.small | 0.07 | 50 |
| ECR, Secrets Manager, logs | – | < 5 |
| **Total** | **≈ 0.31** | **≈ 230** |

Production would add a second NAT gateway, larger nodes and Reserved / Savings Plans pricing.

## Deliberately not included

- Route 53 / ACM / ingress controller (domain-specific).
- Observability backend (Grafana Cloud or a managed Prometheus) — the chart exposes metrics and alert rules.
- CI deploy pipeline with OIDC (an `aws-actions/configure-aws-credentials` step with a role trust to this repository).
