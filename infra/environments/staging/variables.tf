variable "image_tag" {
  description = "Tag of the todo-demo image to run, e.g. sha-abcdef1 or v1.2.3."
  type        = string
}

variable "db_password" {
  description = "Password of this environment's logical database on the shared instance."
  type      = string
  sensitive = true
}