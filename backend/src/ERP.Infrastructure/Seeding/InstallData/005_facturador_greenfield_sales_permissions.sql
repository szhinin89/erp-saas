-- ============================================================================
-- 005_facturador_greenfield_sales_permissions.sql
-- Agrega permisos del pipeline comercial greenfield (cotizaciones y pedidos)
-- al perfil Facturador en todos los tenants existentes.
--
-- Contexto: el commit de Fase 3.6 (2026-05-24) introdujo los bounded contexts
-- Commercial (Quote/SalesOrder) y Fiscal (Invoice) junto con los permisos:
--   sales.quotes.{view,create,update}
--   sales.orders.{view,create,update}
--
-- Los tenants creados ANTES de este deploy tienen esos permisos ausentes en
-- su perfil Facturador. Este script los inserta de forma idempotente.
-- Idempotente: ON CONFLICT (tenant_id, profile_id, permission_key) DO NOTHING.
-- ============================================================================

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
    ('sales.quotes.view'),
    ('sales.quotes.create'),
    ('sales.quotes.update'),
    ('sales.orders.view'),
    ('sales.orders.create'),
    ('sales.orders.update')
) AS t(permission_key)
WHERE ap.name = 'Facturador'
ON CONFLICT (tenant_id, profile_id, permission_key) DO NOTHING;
