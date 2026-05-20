-- =============================================================================
-- Re-apunta FKs de users → identity_users tras unificación de identidad
-- =============================================================================
-- Ejecutar DESPUÉS de:
--   1) scripts/sql/002_unified_documents_schema_and_migration.sql (sección users)
--   2) Validar que cada users.id tiene fila en identity_users (mismo id o email)
--
-- Ajustar nombres de tablas/constraints según su BD (buscar con:
--   SELECT conrelid::regclass, conname FROM pg_constraint
--   WHERE confrelid = 'users'::regclass;
-- =============================================================================

BEGIN;

-- Mapeo explícito (rellenar merged_into cuando el email ya existía en identity_users)
-- INSERT INTO _migration_id_map ... ya lo hace 002 si se ejecutó.

-- Ejemplo: memberships.user_id
UPDATE memberships m
SET user_id = map.new_id
FROM _migration_id_map map
WHERE map.domain = 'USER'
  AND map.legacy_table = 'users'
  AND map.legacy_id = m.user_id
  AND map.new_id IS NOT NULL
  AND map.new_id <> m.user_id;

-- Ejemplo: user_activities (si existe columna user_id)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'user_activities' AND column_name = 'user_id'
    ) THEN
        EXECUTE $sql$
            UPDATE user_activities ua
            SET user_id = map.new_id
            FROM _migration_id_map map
            WHERE map.domain = 'USER'
              AND map.legacy_table = 'users'
              AND map.legacy_id = ua.user_id
              AND map.new_id IS NOT NULL
              AND map.new_id <> ua.user_id
        $sql$;
    END IF;
END $$;

-- Validación
DO $$
DECLARE
    orphan_count INT;
BEGIN
    SELECT COUNT(*) INTO orphan_count
    FROM memberships m
    WHERE NOT EXISTS (SELECT 1 FROM identity_users i WHERE i.id = m.user_id);

    IF orphan_count > 0 THEN
        RAISE EXCEPTION 'memberships con user_id sin identity_users: %', orphan_count;
    END IF;
END $$;

COMMIT;

-- Tras validar aplicación (opcional):
-- ALTER TABLE memberships DROP CONSTRAINT IF EXISTS FK_memberships_users_user_id;
-- ALTER TABLE memberships ADD CONSTRAINT FK_memberships_identity_users
--     FOREIGN KEY (user_id) REFERENCES identity_users(id);
-- DROP TABLE IF EXISTS users CASCADE;
