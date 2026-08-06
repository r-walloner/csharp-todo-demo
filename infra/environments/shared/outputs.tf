output "vpc_private_network_id" {
  value = scaleway_vpc_private_network.main.id
}

output "db_instance_id" {
  value = scaleway_rdb_instance.main.id
}

output "registry_endpoint" {
  value = scaleway_registry_namespace.main.endpoint
}
