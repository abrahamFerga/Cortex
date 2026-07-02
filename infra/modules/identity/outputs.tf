output "id" {
  description = "Resource ID of the user-assigned managed identity."
  value       = azurerm_user_assigned_identity.app.id
}

output "client_id" {
  description = "Client ID of the managed identity (used by the app for token acquisition)."
  value       = azurerm_user_assigned_identity.app.client_id
}

output "principal_id" {
  description = "Principal (object) ID of the managed identity (target of role assignments)."
  value       = azurerm_user_assigned_identity.app.principal_id
}
