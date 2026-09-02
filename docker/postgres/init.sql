-- Enables the pgvector extension in the default database created by the
-- postgres image. Runs once when the repolens_pgdata volume is first initialized
-- (docker compose down -v re-runs it on a fresh volume).
CREATE EXTENSION IF NOT EXISTS vector;
