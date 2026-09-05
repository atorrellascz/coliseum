output "cluster_name" {
  value = module.eks.cluster_name
}

output "cluster_endpoint" {
  value = module.eks.cluster_endpoint
}

output "kubeconfig_command" {
  description = "How to point kubectl at the cluster (always with an explicit context afterwards)."
  value       = "aws eks update-kubeconfig --region ${var.region} --name ${module.eks.cluster_name} --alias ${local.name}"
}

output "ecr_repository_urls" {
  description = "Image registries per host; the release workflow pushes here when ECR replaces GHCR."
  value       = { for k, m in module.ecr : k => m.repository_url }
}

output "redis_primary_endpoint" {
  description = "ElastiCache primary endpoint (TLS, auth token in Secrets Manager)."
  value       = module.elasticache.replication_group_primary_endpoint_address
}

output "app_secret_arn" {
  value = aws_secretsmanager_secret.app.arn
}

output "external_secrets_role_arn" {
  description = "Annotate the external-secrets ServiceAccount with eks.amazonaws.com/role-arn = this value."
  value       = module.external_secrets_irsa.iam_role_arn
}

output "helm_values_snippet" {
  description = "Values for deploy/helm/coliseum that this infrastructure implies."
  value       = <<-EOT
    image:
      registry: ${replace(module.ecr["api"].repository_url, "/coliseum-api", "")}
      api: coliseum-api
      worker: coliseum-worker
      mcp: coliseum-mcp
    redis:
      embedded: false
      external:
        url: "" # taken from the app secret (redisUrl) via External Secrets
    secrets:
      existingSecret: coliseum-app # created by an ExternalSecret from ${aws_secretsmanager_secret.app.name}
    monitoring:
      serviceMonitor: true
  EOT
}
