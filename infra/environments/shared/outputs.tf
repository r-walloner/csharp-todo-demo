output "registry_namespace_id" {
  value = scaleway_registry_namespace.main.id
}

output "vpc_private_network_id" {
  value = scaleway_vpc_private_network.main.id
}

output "rdb_instance_id" {
  value = scaleway_rdb_instance.main.id
}