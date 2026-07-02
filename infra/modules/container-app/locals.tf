# =============================================================================
# locals.tf  (container-app module)
# -----------------------------------------------------------------------------
# Maps the logical Key Vault secret names (the keys of key_vault_secret_ids) to
# the environment variable names the Cortex.Api expects, and to the Container
# App `secret` block name they bind to.
#
# Only secrets present in key_vault_secret_ids are mapped; unknown secrets are
# skipped so this stays robust if the set of seeded secrets changes.
# =============================================================================

locals {
  # logical secret name -> target env var name in the container. These MUST be
  # the configuration keys the app actually binds (see AiOptions, WhatsAppOptions,
  # and the ConnectionStrings the DbContexts resolve) — env-var form uses "__"
  # for the section separator.
  secret_to_env = {
    "platform-connection-string" = "ConnectionStrings__cortex-platform"
    "audit-connection-string"    = "ConnectionStrings__cortex-audit"
    "redis-connection-string"    = "ConnectionStrings__cortex-redis"

    # The active LLM provider's key feeds Ai:ApiKey; which seeded secret that is
    # is selected by var.ai_api_key_secret_name (the others stay in the vault,
    # unmapped, ready for a provider switch).
    (var.ai_api_key_secret_name) = "Ai__ApiKey"

    # WhatsApp channel (Channels:WhatsApp) — enabled/module/etc. are plain env
    # via extra_env; only the credentials are secret-backed.
    "whatsapp-app-secret"   = "Channels__WhatsApp__AppSecret"
    "whatsapp-access-token" = "Channels__WhatsApp__AccessToken"
    "whatsapp-verify-token" = "Channels__WhatsApp__VerifyToken"
  }

  # Build the env list only for secrets that were actually provided.
  secret_env_map = {
    for secret_name, env_name in local.secret_to_env :
    secret_name => {
      secret_name = secret_name
      env_name    = env_name
    }
    if contains(keys(var.key_vault_secret_ids), secret_name)
  }
}
