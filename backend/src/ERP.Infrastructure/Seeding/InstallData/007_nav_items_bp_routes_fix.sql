-- ============================================================================
-- 007_nav_items_bp_routes_fix.sql
-- Alinea el menú de navegación con la unificación BusinessPartner.
--
-- Contexto: las rutas /sales/customers y /purchases/suppliers no existen en el
-- frontend (no hay Route registrado para ellas). Tras la unificación en
-- BusinessPartner (Fase BP-7/9), las páginas canónicas son:
--   /masterdata/customers  → MasterDataCustomersPage  (perm: masterdata.businesspartners.view)
--   /masterdata/suppliers  → MasterDataSuppliersPage  (perm: masterdata.businesspartners.view)
--
-- Cambios:
--   § 1  ui_nav_items    — actualiza route_path y permission_key en el fallback global
--   § 2  commercial_plans — parcha menu_config JSON: corrige rutas BP + agrega
--                           /sales/quotes y /sales/orders donde falten
--
-- Idempotente en todos los casos (WHERE guards).
-- ============================================================================


-- § 1  ui_nav_items (fallback global) ────────────────────────────────────────

-- 1a. Clientes: /sales/customers → /masterdata/customers
UPDATE ui_nav_items
SET route_path     = '/masterdata/customers',
    permission_key = 'masterdata.businesspartners.view'
WHERE route_path = '/sales/customers';

-- 1b. Proveedores: /purchases/suppliers → /masterdata/suppliers
UPDATE ui_nav_items
SET route_path     = '/masterdata/suppliers',
    permission_key = 'masterdata.businesspartners.view'
WHERE route_path = '/purchases/suppliers';

-- 1c. Cotizaciones: insertar si aún no existe (DBs anteriores a 002 actualizado)
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id",
       '/sales/quotes', 'app.nav.sales.quotes', 'Cotizaciones',
       15, 'sales', 'sales.quotes.view', true
FROM ui_nav_groups g
WHERE g.code = 'sales'
  AND NOT EXISTS (
    SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = '/sales/quotes'
  );

-- 1d. Pedidos: insertar si aún no existe
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id",
       '/sales/orders', 'app.nav.sales.orders', 'Pedidos',
       17, 'sales', 'sales.orders.view', true
FROM ui_nav_groups g
WHERE g.code = 'sales'
  AND NOT EXISTS (
    SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = '/sales/orders'
  );


-- § 2  commercial_plans — parchear menu_config JSON ──────────────────────────

-- 2a. Corregir rutas BP legacy y sus permissionKey en todos los planes
UPDATE commercial_plans
SET menu_config = REPLACE(
    REPLACE(
        REPLACE(
            REPLACE(
                menu_config::text,
                '"/sales/customers"',
                '"/masterdata/customers"'
            ),
            '"perm:sales.customers.view"',
            '"perm:masterdata.businesspartners.view"'
        ),
        '"/purchases/suppliers"',
        '"/masterdata/suppliers"'
    ),
    '"perm:purchases.suppliers.view"',
    '"perm:masterdata.businesspartners.view"'
)::jsonb
WHERE menu_config IS NOT NULL
  AND (
      menu_config::text LIKE '%/sales/customers%'
   OR menu_config::text LIKE '%/purchases/suppliers%'
  );

-- 2b. Agregar /sales/quotes al grupo sales si no existe en el plan
UPDATE commercial_plans
SET menu_config = (
    SELECT jsonb_agg(
        CASE
            WHEN grp->>'code' = 'sales'
             AND NOT (grp->'items' @> '[{"routePath": "/sales/quotes"}]') THEN
                jsonb_set(
                    grp,
                    '{items}',
                    (grp->'items') || jsonb_build_object(
                        'routePath',        '/sales/quotes',
                        'labelKey',         'app.nav.sales.quotes',
                        'displayLabel',     'Cotizaciones',
                        'sortOrder',        15,
                        'moduleKey',        'sales',
                        'permissionKey',    'perm:sales.quotes.view',
                        'permissionKeysAny', null,
                        'itemRoles',        null,
                        'children',         null,
                        'icon',             '📝'
                    )
                )
            ELSE grp
        END
    )
    FROM jsonb_array_elements(menu_config) AS grp
)
WHERE menu_config IS NOT NULL
  AND menu_config::text LIKE '%"code": "sales"%'
  AND NOT menu_config::text LIKE '%/sales/quotes%';

-- 2c. Agregar /sales/orders al grupo sales si no existe en el plan
UPDATE commercial_plans
SET menu_config = (
    SELECT jsonb_agg(
        CASE
            WHEN grp->>'code' = 'sales'
             AND NOT (grp->'items' @> '[{"routePath": "/sales/orders"}]') THEN
                jsonb_set(
                    grp,
                    '{items}',
                    (grp->'items') || jsonb_build_object(
                        'routePath',        '/sales/orders',
                        'labelKey',         'app.nav.sales.orders',
                        'displayLabel',     'Pedidos',
                        'sortOrder',        17,
                        'moduleKey',        'sales',
                        'permissionKey',    'perm:sales.orders.view',
                        'permissionKeysAny', null,
                        'itemRoles',        null,
                        'children',         null,
                        'icon',             '📋'
                    )
                )
            ELSE grp
        END
    )
    FROM jsonb_array_elements(menu_config) AS grp
)
WHERE menu_config IS NOT NULL
  AND menu_config::text LIKE '%"code": "sales"%'
  AND NOT menu_config::text LIKE '%/sales/orders%';
