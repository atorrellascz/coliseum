# Coliseum on AWS: VPC (3 AZ) -> EKS (IRSA, one managed node group) -> ECR (api, worker, mcp) ->
# ElastiCache Redis 7 (multi-AZ, noeviction, TLS + auth token) -> Secrets Manager -> IRSA role for External Secrets.
# Everything uses the official terraform-aws-modules; outputs feed the Helm chart (see outputs.tf).

# ---------------------------------------------------------------- network
module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "~> 5.13"

  name = local.name
  cidr = var.vpc_cidr
  azs  = slice(data.aws_availability_zones.available.names, 0, 3)

  private_subnets = [for i in range(3) : cidrsubnet(var.vpc_cidr, 4, i)]
  public_subnets  = [for i in range(3) : cidrsubnet(var.vpc_cidr, 4, i + 8)]

  enable_nat_gateway   = true
  single_nat_gateway   = var.environment != "prod"
  enable_dns_hostnames = true

  # Tags the AWS load balancer controller and EKS use to place load balancers.
  public_subnet_tags = {
    "kubernetes.io/role/elb"              = 1
    "kubernetes.io/cluster/${local.name}" = "shared"
  }
  private_subnet_tags = {
    "kubernetes.io/role/internal-elb"     = 1
    "kubernetes.io/cluster/${local.name}" = "shared"
  }
}

# ---------------------------------------------------------------- kubernetes
module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "~> 20.24"

  cluster_name    = local.name
  cluster_version = var.eks_version

  vpc_id                   = module.vpc.vpc_id
  subnet_ids               = module.vpc.private_subnets
  control_plane_subnet_ids = module.vpc.private_subnets

  cluster_endpoint_public_access           = true
  enable_irsa                              = true
  enable_cluster_creator_admin_permissions = true

  cluster_addons = {
    coredns                = {}
    kube-proxy             = {}
    vpc-cni                = {}
    eks-pod-identity-agent = {}
  }

  eks_managed_node_groups = {
    default = {
      instance_types = var.node_instance_types
      min_size       = var.node_min_size
      max_size       = var.node_max_size
      desired_size   = var.node_desired_size

      # Cluster Autoscaler discovery tags.
      tags = {
        "k8s.io/cluster-autoscaler/enabled"       = "true"
        "k8s.io/cluster-autoscaler/${local.name}" = "owned"
      }
    }
  }
}

# ---------------------------------------------------------------- images
module "ecr" {
  source  = "terraform-aws-modules/ecr/aws"
  version = "~> 2.3"

  for_each = toset(["api", "worker", "mcp"])

  repository_name                 = "${var.project}-${each.key}"
  repository_image_tag_mutability = "IMMUTABLE"
  repository_image_scan_on_push   = true
  repository_force_delete         = var.environment != "prod"

  repository_lifecycle_policy = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "Keep the last ${var.ecr_images_to_keep} images"
      selection = {
        tagStatus   = "any"
        countType   = "imageCountMoreThan"
        countNumber = var.ecr_images_to_keep
      }
      action = { type = "expire" }
    }]
  })
}

# ---------------------------------------------------------------- redis
resource "random_password" "redis_auth_token" {
  length  = 48
  special = false
}

module "elasticache" {
  source  = "terraform-aws-modules/elasticache/aws"
  version = "~> 1.4"

  replication_group_id = local.name
  description          = "Coliseum primary store: players, battle queue, leaderboard"

  engine         = "redis"
  engine_version = var.redis_engine_version
  node_type      = var.redis_node_type

  # Two nodes across AZs with automatic failover: the stream and balances survive a node loss.
  num_cache_clusters         = 2
  multi_az_enabled           = true
  automatic_failover_enabled = true
  apply_immediately          = var.environment != "prod"

  transit_encryption_enabled = true
  at_rest_encryption_enabled = true
  auth_token                 = random_password.redis_auth_token.result

  # noeviction is not optional (SUP-10): evicting a battle record would break settlement idempotency.
  create_parameter_group = true
  parameter_group_family = "redis7"
  parameters = [
    { name = "maxmemory-policy", value = "noeviction" },
  ]

  vpc_id     = module.vpc.vpc_id
  subnet_ids = module.vpc.private_subnets

  # Only the EKS nodes may talk to Redis.
  security_group_rules = {
    ingress_eks_nodes = {
      description                  = "Redis from EKS nodes"
      from_port                    = 6379
      to_port                      = 6379
      ip_protocol                  = "tcp"
      referenced_security_group_id = module.eks.node_security_group_id
    }
  }
}

# ---------------------------------------------------------------- secrets
resource "random_password" "signing_key" {
  length  = 64
  special = false
}

resource "random_password" "api_key" {
  length  = 40
  special = false
}

resource "random_password" "mcp_client_key" {
  length  = 40
  special = false
}

# One JSON secret with the keys the Helm chart expects (secrets.existingSecret): signingKey, apiKey, mcpClientKey,
# plus the Redis URL in StackExchange.Redis syntax so no host ever assembles a connection string.
resource "aws_secretsmanager_secret" "app" {
  name                    = "${local.name}/app"
  recovery_window_in_days = var.environment == "prod" ? 30 : 0
}

resource "aws_secretsmanager_secret_version" "app" {
  secret_id = aws_secretsmanager_secret.app.id
  secret_string = jsonencode({
    signingKey   = random_password.signing_key.result
    apiKey       = random_password.api_key.result
    mcpClientKey = random_password.mcp_client_key.result
    redisUrl     = "${module.elasticache.replication_group_primary_endpoint_address}:6379,ssl=true,password=${random_password.redis_auth_token.result}"
  })
}

# IRSA role for the External Secrets Operator: pods get AWS credentials through the cluster OIDC provider, never as
# static keys. The role may read only the Coliseum secret.
module "external_secrets_irsa" {
  source  = "terraform-aws-modules/iam/aws//modules/iam-role-for-service-accounts-eks"
  version = "~> 5.44"

  role_name                             = "${local.name}-external-secrets"
  attach_external_secrets_policy        = true
  external_secrets_secrets_manager_arns = [aws_secretsmanager_secret.app.arn]
  external_secrets_ssm_parameter_arns   = []
  external_secrets_kms_key_arns         = []

  oidc_providers = {
    main = {
      provider_arn               = module.eks.oidc_provider_arn
      namespace_service_accounts = ["external-secrets:external-secrets"]
    }
  }
}
