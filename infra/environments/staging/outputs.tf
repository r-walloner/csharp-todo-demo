output "app_container_public_endpoint" {
    value = module.app.app_container_public_endpoint
    description = "The public http(s) endpoint the application is reachable at."
}