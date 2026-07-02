# =============================================================================
# providers.tf
# -----------------------------------------------------------------------------
# Provider configuration. Credentials are NOT set here; they are provided by the
# environment:
#   - Locally: `az login` (Azure CLI auth) is picked up automatically.
#   - In CI: OIDC federation via azure/login@v2 sets ARM_CLIENT_ID,
#     ARM_TENANT_ID, ARM_SUBSCRIPTION_ID and ARM_USE_OIDC=true in the env.
# =============================================================================

provider "azurerm" {
  features {}

  # When false (the default in v4) Terraform will not attempt to register
  # resource providers on the subscription. Flip via TF var/env if your SP has
  # the rights and you want auto-registration.
  resource_provider_registrations = "none"
}

provider "azuread" {
  # Tenant is inferred from the Azure CLI / OIDC environment. Override with the
  # ARM_TENANT_ID env var or `tenant_id` here if you target a different tenant
  # for app registrations than for resource deployment.
}
