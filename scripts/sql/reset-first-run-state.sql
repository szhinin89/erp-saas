-- Reinicio manual del first-run + eliminación del SuperAdmin (PostgreSQL).
-- Sustituye al endpoint POST /api/dev/reset-first-run solo cuando no puedas usar la API.
-- Orden: refresh_tokens que apunten a SuperAdmin, usuarios SuperAdmin, estado first-run.
-- Nombres reales de columnas: setup_token_hash, setup_token_expiry_utc, completed_at (no token_hash).

BEGIN;

DELETE FROM refresh_tokens
WHERE user_type = 'SuperAdmin'
   OR user_id IN (SELECT id FROM users WHERE role = 'SuperAdmin');

DELETE FROM users WHERE role = 'SuperAdmin';

UPDATE first_run_setup_state
SET is_first_run              = true,
    setup_token_hash          = NULL,
    setup_token_expiry_utc    = NULL,
    completed_at              = NULL,
    updated_at                = NOW(),
    updated_by                = '11111111-1111-1111-1111-111111111111'::uuid;

COMMIT;
