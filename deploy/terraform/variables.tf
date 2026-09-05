variable "region" {
  description = "AWS region."
  type        = string
  default     = "eu-west-1"
}

variable "project" {
  description = "Project name used as a prefix for every resource."
  type        = string
  default     = "coliseum"
}

variable "environment" {
  description = "Environment name (dev, staging, prod). Non-prod environments use a single NAT gateway and allow force-deleting ECR repositories."
  type        = string
  default     = "dev"

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "environment must be dev, staging or prod."
  }
}

variable "vpc_cidr" {
  description = "CIDR block of the VPC. Three private and three public subnets are carved out of it."
  type        = string
  default     = "10.40.0.0/16"
}

variable "eks_version" {
  description = "Kubernetes version of the EKS control plane."
  type        = string
  default     = "1.31"
}

variable "node_instance_types" {
  description = "Instance types of the managed node group."
  type        = list(string)
  default     = ["t3.medium"]
}

variable "node_min_size" {
  type    = number
  default = 2
}

variable "node_max_size" {
  type    = number
  default = 5
}

variable "node_desired_size" {
  type    = number
  default = 2
}

variable "redis_node_type" {
  description = "ElastiCache node type. cache.t4g.small has 10x headroom for the exercise's load (docs/sre.md)."
  type        = string
  default     = "cache.t4g.small"
}

variable "redis_engine_version" {
  type    = string
  default = "7.1"
}

variable "ecr_images_to_keep" {
  description = "How many tagged images each ECR repository keeps before the lifecycle policy expires the oldest."
  type        = number
  default     = 30
}
