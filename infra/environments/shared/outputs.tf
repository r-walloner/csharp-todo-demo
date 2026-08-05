output "vpc_private_network_id" {
  value = scaleway_vpc_private_network.main.id
}

output "db_instance_id" {
  value = scaleway_rdb_instance.main.id
}

output "db_ip" {
  value = scaleway_rdb_instance.main.private_network[0].ip
}

output "db_port" {
  value = scaleway_rdb_instance.main.private_network[0].port
}

output "registry_endpoint" {
  value = scaleway_registry_namespace.main.endpoint
}
