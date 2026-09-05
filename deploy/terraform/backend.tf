# Remote state. Commented out so `terraform init -backend=false` works in CI without AWS credentials.
# To use it: create the bucket (versioned, encrypted) and the DynamoDB table (partition key LockID) once, then
# uncomment and run `terraform init -migrate-state`.
#
# terraform {
#   backend "s3" {
#     bucket         = "coliseum-terraform-state"
#     key            = "coliseum/dev/terraform.tfstate"
#     region         = "eu-west-1"
#     dynamodb_table = "coliseum-terraform-locks"
#     encrypt        = true
#   }
# }
