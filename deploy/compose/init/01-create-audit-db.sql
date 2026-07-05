-- Runs once on first Postgres start (empty volume): the append-only audit store lives in its
-- own database, separated from operational data like production topologies expect.
CREATE DATABASE cortex_audit;
