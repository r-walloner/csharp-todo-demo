output "app_container_public_endpoint" {
    value = scaleway_container.app.public_endpoint
    description = "The public http(s) endpoint the application is reachable at."
}