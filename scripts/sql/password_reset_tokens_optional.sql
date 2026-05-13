-- Opcional: solo si la base ya tenía una tabla legacy sin columnas alineadas con el modelo actual.
-- En instalaciones nuevas use la migración EF: AddPasswordResetTokens (tabla password_reset_tokens).

-- Crear tabla completa (PostgreSQL), solo si no existe:
-- CREATE TABLE IF NOT EXISTS password_reset_tokens (
--   id uuid NOT NULL PRIMARY KEY,
--   token_hash character varying(128) NOT NULL,
--   user_id uuid NOT NULL,
--   user_kind character varying(20) NOT NULL,
--   tenant_id uuid NULL,
--   expires_at timestamp with time zone NOT NULL,
--   used boolean NOT NULL,
--   created_at timestamp with time zone NOT NULL
-- );
-- CREATE UNIQUE INDEX IF NOT EXISTS ix_password_reset_tokens_hash ON password_reset_tokens (token_hash);
-- CREATE INDEX IF NOT EXISTS ix_password_reset_tokens_user ON password_reset_tokens (user_id, user_kind, tenant_id);

-- Si la tabla existía sin tenant_id nullable:
-- ALTER TABLE password_reset_tokens ADD COLUMN IF NOT EXISTS tenant_id uuid NULL;
