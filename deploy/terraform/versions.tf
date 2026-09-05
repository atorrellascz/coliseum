# Terraform and provider constraints. Validated in CI with `terraform init -backend=false && terraform validate`;
# never applied from this repository (an EKS cluster costs real money), see README.md.
terraform {
  required_version = ">= 1.9"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.70"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}
