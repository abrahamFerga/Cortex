output "fqdn" {
  description = "Public FQDN of the container app ingress."
  value       = azurerm_container_app.api.ingress[0].fqdn
}

output "app_name" {
  description = "Name of the container app."
  value       = azurerm_container_app.api.name
}

output "environment_id" {
  description = "Resource ID of the Container App Environment."
  value       = azurerm_container_app_environment.this.id
}

output "latest_revision_name" {
  description = "Name of the latest revision."
  value       = azurerm_container_app.api.latest_revision_name
}
