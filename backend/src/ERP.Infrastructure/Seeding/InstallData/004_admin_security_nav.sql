-- ============================================================================
-- 004_admin_security_nav.sql
-- Agrega /admin/security al grupo "admin":
--   § 1  ui_nav_items  — fallback global
--   § 2  commercial_plans — starter menu_config via jsonb_set
-- Idempotente en ambos casos.
-- ============================================================================

-- § 1  ui_nav_items (fallback global) ─────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id",
       '/admin/security', 'app.nav.item.admin.security', 'Seguridad',
       40, 'admin', null, true
FROM ui_nav_groups g
WHERE g.code = 'admin'
  AND NOT EXISTS (
    SELECT 1 FROM ui_nav_items i
    WHERE i.group_id = g."Id" AND i.route_path = '/admin/security'
  );

-- § 2  starter menu_config (jsonb_set sobre grupo admin) ──────────────────────
UPDATE commercial_plans
SET menu_config = (
    SELECT jsonb_agg(
        CASE
            WHEN grp->>'code' = 'admin' THEN
                jsonb_set(
                    grp,
                    '{items}',
                    (grp->'items') || jsonb_build_object(
                        'routePath',         '/admin/security',
                        'labelKey',          'app.nav.item.admin.security',
                        'displayLabel',      'Seguridad',
                        'sortOrder',         40,
                        'moduleKey',         'admin',
                        'permissionKey',     null,
                        'permissionKeysAny', null,
                        'itemRoles',         jsonb_build_array('SuperAdmin', 'Admin'),
                        'children',          null,
                        'icon',              '🔒'
                    )
                )
            ELSE grp
        END
        ORDER BY (grp->>'sortOrder')::int
    )
    FROM jsonb_array_elements(menu_config) AS grp
)
WHERE code = 'starter'
  AND menu_config IS NOT NULL
  AND menu_config::text NOT LIKE '%/admin/security%';
