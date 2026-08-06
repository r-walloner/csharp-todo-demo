variable "environment" {
  description = "Short name for this instantiation, e.g. staging or production."
  type        = string
}

variable "vpc_private_network_id" {
  description = "ID of the VPC private network to attach the container to (from infra/environments/shared's outputs)."
  type        = string
}

variable "registry_endpoint" {
  description = "Endpoint of the shared registry namespace, e.g. rg.fr-par.scw.cloud/todo-demo (from shared's outputs)."
  type        = string
}

variable "image_tag" {
  description = "Tag of the todo-demo image to run, e.g. sha-abcdef1 or v1.2.3."
  type        = string
}

variable "db_instance_id" {
  description = "ID of the shared DB instance (from infra/environments/shared's outputs)."
  type        = string
}

variable "db_name" {
  description = "Name of this environment's logical database on the shared instance."
  type        = string
}

variable "db_user" {
  description = "Username of this environment's logical database on the shared instance."
  type        = string
}

variable "db_password" {
  description = "Password of this environment's logical database on the shared instance."
  type      = string
  sensitive = true
}
