output "id" {
  description = "Resource ID of the CI/CD user-assigned managed identity."
  value       = azurerm_user_assigned_identity.cicd.id
}

output "client_id" {
  description = "Client ID of the CI/CD identity. Set as the AZURE_CLIENT_ID GitHub secret."
  value       = azurerm_user_assigned_identity.cicd.client_id
}

output "principal_id" {
  description = "Principal (object) ID of the CI/CD identity."
  value       = azurerm_user_assigned_identity.cicd.principal_id
}

output "federated_subjects" {
  description = "Map of credential key -> OIDC subject claim configured for trust."
  value       = local.all_subjects
}
