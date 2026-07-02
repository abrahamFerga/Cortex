output "workspace_id" {
  description = "Resource ID of the Log Analytics workspace."
  value       = azurerm_log_analytics_workspace.this.id
}

output "workspace_customer_id" {
  description = "Workspace (customer) GUID for Log Analytics."
  value       = azurerm_log_analytics_workspace.this.workspace_id
}

output "appinsights_connection_string" {
  description = "Application Insights connection string for OpenTelemetry export."
  value       = azurerm_application_insights.this.connection_string
  sensitive   = true
}

output "appinsights_instrumentation_key" {
  description = "Application Insights instrumentation key (legacy; prefer connection string)."
  value       = azurerm_application_insights.this.instrumentation_key
  sensitive   = true
}
