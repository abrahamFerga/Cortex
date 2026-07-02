# =============================================================================
# versions.tf
# -----------------------------------------------------------------------------
# Terraform + provider version constraints and remote state backend.
#
# The backend uses PARTIAL configuration: the concrete values (storage account,
# container, key, etc.) are supplied at `terraform init` time via
# `-backend-config=...` so that no environment-specific or sensitive values are
# committed to source control. The CI/CD pipeline (deploy.yml / pr-check.yml)
# wires these from GitHub secrets.
# =============================================================================

terraform {
  required_version = ">= 1.9"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Remote state in an Azure Storage Account.
  # Partial config: pass the rest at init, e.g.
  #   terraform init \
  #     -backend-config="resource_group_name=$TF_BACKEND_RESOURCE_GROUP" \
  #     -backend-config="storage_account_name=$TF_BACKEND_STORAGE_ACCOUNT" \
  #     -backend-config="container_name=$TF_BACKEND_CONTAINER" \
  #     -backend-config="key=$TF_STATE_KEY"
  backend "azurerm" {
    # Authenticate to the backend using the same OIDC/Managed Identity flow as
    # the providers. With azurerm provider v4 this is enabled by default in CI
    # when ARM_USE_OIDC / ARM_USE_AZUREAD_AUTH are set in the environment.
    use_oidc = true
  }
}
