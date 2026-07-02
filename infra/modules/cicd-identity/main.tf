# =============================================================================
# module: cicd-identity
# -----------------------------------------------------------------------------
# User-assigned managed identity + Entra federated identity credentials for
# GitHub Actions OIDC. No client secrets are ever stored — GitHub presents an
# OIDC token whose `subject` claim must match a federated credential below.
#
# Role assignments grant the pipeline exactly what it needs:
#   - Contributor on the resource group  (terraform apply)
#   - AcrPush on the registry             (push images)
#   - Key Vault Secrets Officer on the KV (seed/rotate secrets during apply)
#
# Set the resulting client_id as the AZURE_CLIENT_ID GitHub secret.
# =============================================================================

resource "azurerm_user_assigned_identity" "cicd" {
  name                = "${var.name_prefix}-cicd-id"
  resource_group_name = var.resource_group_name
  location            = var.location
  tags                = var.tags
}

# ---------------------------------------------------------------------------
# Federated identity credentials (OIDC subjects)
# ---------------------------------------------------------------------------
# GitHub's issuer + audience are fixed. Each credential pins a subject claim.

locals {
  github_issuer   = "https://token.actions.githubusercontent.com"
  github_audience = ["api://AzureADTokenExchange"]
  repo            = "${var.github_owner}/${var.github_repo}"

  # Static subjects: pushes to main and any pull_request event.
  static_subjects = {
    "main"         = "repo:${local.repo}:ref:refs/heads/main"
    "pull-request" = "repo:${local.repo}:pull_request"
  }

  # Per-GitHub-environment subjects (e.g. production approval gate).
  env_subjects = {
    for env in var.github_environments :
    "env-${env}" => "repo:${local.repo}:environment:${env}"
  }

  all_subjects = merge(local.static_subjects, local.env_subjects)
}

resource "azurerm_federated_identity_credential" "github" {
  for_each = local.all_subjects

  name                = "gh-${each.key}"
  resource_group_name = var.resource_group_name
  parent_id           = azurerm_user_assigned_identity.cicd.id
  audience            = local.github_audience
  issuer              = local.github_issuer
  subject             = each.value
}

# ---------------------------------------------------------------------------
# Role assignments
# ---------------------------------------------------------------------------
resource "azurerm_role_assignment" "rg_contributor" {
  scope                = var.resource_group_id
  role_definition_name = "Contributor"
  principal_id         = azurerm_user_assigned_identity.cicd.principal_id
}

resource "azurerm_role_assignment" "acr_push" {
  scope                = var.acr_id
  role_definition_name = "AcrPush"
  principal_id         = azurerm_user_assigned_identity.cicd.principal_id
}

resource "azurerm_role_assignment" "kv_secrets_officer" {
  scope                = var.key_vault_id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = azurerm_user_assigned_identity.cicd.principal_id
}
