-- ============================================================================
-- 008_facturador_masterdata_bp_permissions.sql
-- Sustituye los permisos legacy sales.customers.* / purchases.suppliers.*
-- por masterdata.businesspartners.* en los perfiles Facturador existentes.
--
-- Contexto: tras la unificación en BusinessPartner (BP-7/9), las páginas de
-- Clientes y Proveedores requieren masterdata.businesspartners.* — no los
-- permisos legacy. Los perfiles Facturador creados antes de este script
-- tienen sales.customers.* pero NO masterdata.businesspartners.*.
--
-- Cambios:
--   § 1  Insertar masterdata.businesspartners.{view,create,update,disable}
--         en todos los perfiles Facturador (ON CONFLICT DO NOTHING).
--   § 2  Eliminar sales.customers.{view,create,update,delete} del perfil
--         Facturador (estas keys no tienen controlador activo; causan confusión
--         en la UI de Perfiles).
--
-- NO toca purchases.suppliers.* — ese perfil (Facturador) nunca los tuvo.
-- Idempotente.
-- ============================================================================

-- § 1  Agregar masterdata.businesspartners.* al perfil Facturador ─────────────

INSERT INTO access_profile_permissions
    (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
SELECT
    gen_random_uuid()                                    AS id,
    ap.tenant_id,
    ap.id                                                AS profile_id,
    t.permission_key,
    TRUE                                                 AS is_allowed,
    NOW()                                                AS created_at,
    '00000000-0000-0000-0000-000000000000'::UUID         AS created_by
FROM access_profiles ap
CROSS JOIN (VALUES
    ('masterdata.businesspartners.view'),
    ('masterdata.businesspartners.create'),
    ('masterdata.businesspartners.update'),
    ('masterdata.businesspartners.disable')
) AS t(permission_key)
WHERE ap.name = 'Facturador'
ON CONFLICT (tenant_id, profile_id, permission_key) DO NOTHING;


-- § 2  Eliminar sales.customers.* del perfil Facturador ──────────────────────
-- (no tienen controlador activo tras la unificación BP)

DELETE FROM access_profile_permissions app
USING access_profiles ap
WHERE app.profile_id  = ap.id
  AND ap.name         = 'Facturador'
  AND app.permission_key IN (
      'sales.customers.view',
      'sales.customers.create',
      'sales.customers.update',
      'sales.customers.delete'
  );
