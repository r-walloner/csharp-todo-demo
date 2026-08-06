terraform {
  required_providers {
    scaleway = { source = "scaleway/scaleway" }
  }
  backend "s3" {
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

resource "scaleway_account_ssh_key" "cd-runner-key" {
  name = "robin-todo-demo-cd-runner-key"
  public_key = var.cd_runner_public_ssh_key
}

resource "scaleway_registry_namespace" "main" {
  name = "robin-todo-demo-cr"
  is_public = false
}

resource "scaleway_vpc_private_network" "main" {
  name = "robin-todo-demo-net"
}

resource "scaleway_rdb_instance" "main" {
  name = "robin-todo-demo-db"
  engine = "PostgreSQL-17"
  node_type = "db-dev-s"
  is_ha_cluster = false
  volume_type = "sbs_5k"
  volume_size_in_gb = 5
  private_network {
    pn_id = scaleway_vpc_private_network.main.id
  }
}
