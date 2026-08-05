terraform {
  required_providers {
    scaleway = { source = "scaleway/scaleway" }
  }
  backend "s3" {
    bucket = "robin-todo-demo-state"
    key = "production/terraform.tfstate"
    region = "fr-par"
    endpoints = { s3 = "https://s3.fr-par.scw.cloud" }
    skip_credentials_validation = true
    skip_region_validation = true
    skip_requesting_account_id = true
  }
}

data "terraform_remote_state" "shared" {
# Reference the remote state created by the shared environment
  backend = "s3"
  config = {
    bucket = "robin-todo-demo-state"
    key = "shared/terraform.tfstate"
    region = "fr-par"
    endpoints = { s3 = "https://s3.fr-par.scw.cloud" }
    skip_credentials_validation = true
    skip_region_validation = true
    skip_requesting_account_id = true
  }
}

provider "scaleway" {
  region = "fr-par"
  zone = "fr-par-1"
}

module "app" {
    source = "../../modules/app"

    environment = "production"
    vpc_private_network_id = data.terraform_remote_state.shared.outputs.vpc_private_network_id
    registry_endpoint = data.terraform_remote_state.shared.outputs.registry_endpoint
    image_tag = var.image_tag
    db_instance_id = data.terraform_remote_state.shared.outputs.db_instance_id
    db_ip = data.terraform_remote_state.shared.outputs.db_ip
    db_port = data.terraform_remote_state.shared.outputs.db_port
    db_name = "robin_todo_demo_production"
    db_user = "robin_todo_demo_production_user"
    db_password = var.db_password
}