# =============================================================================
# module: cache
# -----------------------------------------------------------------------------
# Azure Cache for Redis. Used as the SignalR backplane, distributed cache, and
# rate-limiting store. The primary connection string is surfaced as an output so
# the keyvault module can store it as a secret.
# =============================================================================

resource "azurerm_redis_cache" "this" {
  name                = "${var.name_prefix}-redis"
  resource_group_name = var.resource_group_name
  location            = var.location

  capacity = var.capacity
  family   = var.family
  sku_name = var.sku_name

  # Enforce TLS-only access.
  non_ssl_port_enabled = false
  minimum_tls_version  = "1.2"

  redis_configuration {
    # No special config by default; the App handles eviction policy needs via
    # key TTLs. Extend here for maxmemory-policy, etc.
  }

  tags = var.tags
}
