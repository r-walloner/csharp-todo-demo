output "registry_namespace_id" {
  value = scaleway_registry_namespace.main.id
}

output "vpc_private_network_id" {
  value = scaleway_vpc_private_network.main.id
}

output "rdb_instance_id" {
  value = scaleway_rdb_instance.main.id
}

output "rdb_instance_private_ip" {
  value = scaleway_rdb_instance.main.private_network[0].ip
  description = "The ip address of the DB instance on the private network. Used by the CI/CD pipeline to run migrations."
}

output "rdb_instance_private_port" {
  value = scaleway_rdb_instance.main.private_network[0].port
  description = "The port of the DB instance on the private network. Used by the CI/CD pipeline to run migrations."
}