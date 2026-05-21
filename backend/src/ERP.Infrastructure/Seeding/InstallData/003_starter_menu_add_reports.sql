-- ============================================================================
-- 003_starter_menu_add_reports.sql
-- Agrega carpeta "Reportes" al grupo "sales" del plan starter.
-- Idempotente: solo actúa si /reportes/ no está ya en menu_config.
--
-- Técnica: jsonb_set + jsonb_array_elements para modificar solo el grupo
-- "sales" sin reescribir todo el JSON del plan.
-- ============================================================================

UPDATE saas_plans
SET menu_config = (
    SELECT jsonb_agg(
        CASE
            WHEN grp->>'code' = 'sales' THEN
                jsonb_set(
                    grp,
                    '{items}',
                    (grp->'items') || jsonb_build_object(
                        'routePath',       '',
                        'labelKey',        'app.nav.item.sales.reports',
                        'displayLabel',    'Reportes',
                        'sortOrder',       50,
                        'moduleKey',       'sales',
                        'permissionKey',   null,
                        'permissionKeysAny', null,
                        'itemRoles',       null,
                        'icon',            'bar_chart',
                        'children',        jsonb_build_array(
                            jsonb_build_object(
                                'routePath',        '/reportes/ventas',
                                'labelKey',         'app.nav.item.sales.report-ventas',
                                'displayLabel',     'Reporte de ventas',
                                'sortOrder',        10,
                                'moduleKey',        'sales',
                                'permissionKey',    'perm:sales.invoices.view',
                                'permissionKeysAny', null,
                                'itemRoles',        null,
                                'children',         null,
                                'icon',             'receipt_long'
                            )
                        )
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
  AND menu_config::text NOT LIKE '%/reportes/%';
