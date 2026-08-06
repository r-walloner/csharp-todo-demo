data "scaleway_rdb_instance" "shared" {
  # Reference the shared RDB instance
  instance_id = var.db_instance_id
}

resource "scaleway_container_namespace" "main" {
  name = "robin-todo-demo-${var.environment}"
}

resource "scaleway_container" "app" {
  name = "robin-todo-demo-${var.environment}-app"
  namespace_id = scaleway_container_namespace.main.id
  image = "${var.registry_endpoint}/todo-demo:${var.image_tag}"
  port = 5000 # Internal porth within the container
  privacy = "public"
  cpu_limit = 100 # mVCPU
  memory_limit_bytes = 256000000 # 256 MB
  min_scale = 1
  max_scale = 1

  environment_variables = {
    "ASPNETCORE_ENVIRONMENT" = "Production" # TODO: maybe make this configurable per environment
  }
  secret_environment_variables = {
    "ConnectionStrings__TodoDb" = "Host=${data.scaleway_rdb_instance.shared.private_network[0].ip};Port=${data.scaleway_rdb_instance.shared.private_network[0].port};Database=${var.db_name};Username=${var.db_user};Password=${var.db_password};SSL Mode=Require;Maximum Pool Size=10"
  }

  private_network_id = var.vpc_private_network_id

  liveness_probe {
    http {
      path = "/health/ready"
    }
    interval = "5s"
    timeout = "1s"
    failure_threshold = 3
  }
}

resource "scaleway_rdb_database" "main" {
  instance_id = data.scaleway_rdb_instance.shared.id
  name = var.db_name # TODO: generate the name automatically from the environment name
}

resource "scaleway_rdb_user" "main" {
  instance_id = data.scaleway_rdb_instance.shared.id
  name = var.db_user # TODO: generate the name automatically from the environment name
  password = var.db_password # TODO: security best practice: use password_wo and password_wo_version to avoid string password in state file (see https://search.opentofu.org/provider/hashicorp/scaleway/latest/docs/resources/rdb_user)
  is_admin = false
}

resource "scaleway_rdb_privilege" "main" {
  instance_id = data.scaleway_rdb_instance.shared.id
  database_name = scaleway_rdb_database.main.name
  user_name = scaleway_rdb_user.main.name
  permission = "all"
}
