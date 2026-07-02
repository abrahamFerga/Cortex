# =============================================================================
# module: monitoring
# -----------------------------------------------------------------------------
# Log Analytics workspace + Application Insights (workspace-based). The app
# exports OpenTelemetry to App Insights via APPLICATIONINSIGHTS_CONNECTION_STRING.
# =============================================================================

resource "azurerm_log_analytics_workspace" "this" {
  name                = "${var.name_prefix}-log"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "PerGB2018"
  retention_in_days   = var.retention_in_days
  tags                = var.tags
}

resource "azurerm_application_insights" "this" {
  name                = "${var.name_prefix}-appi"
  resource_group_name = var.resource_group_name
  location            = var.location
  application_type    = "web"
  workspace_id        = azurerm_log_analytics_workspace.this.id
  tags                = var.tags
}
