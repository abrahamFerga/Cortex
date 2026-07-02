output "hostname" {
  description = "Hostname of the Redis instance."
  value       = azurerm_redis_cache.this.hostname
}

output "ssl_port" {
  description = "TLS port for Redis."
  value       = azurerm_redis_cache.this.ssl_port
}

output "primary_connection_string" {
  description = "Primary StackExchange.Redis connection string (consumed by keyvault module)."
  value       = azurerm_redis_cache.this.primary_connection_string
  sensitive   = true
}
