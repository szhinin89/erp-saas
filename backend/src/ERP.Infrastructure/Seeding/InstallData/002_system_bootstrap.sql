-- ============================================================================
-- 002_system_bootstrap.sql
-- Bootstrap de sistema — ejecutado UNA VEZ por checksum via InstallDataBootstrapService.
-- Idempotente: CREATE OR REPLACE en funciones; ON CONFLICT DO NOTHING en tablas.
-- No usar BEGIN/COMMIT: el servicio ya ejecuta dentro de una transacción propia.
--
-- Secciones:
--   § 1  Función erp_seed_tenant_default_profiles()  — perfiles por tenant
--   § 2  Grupos de navegación global (ui_nav_groups)  — 9 grupos
--   § 3  Ítems de navegación global  (ui_nav_items)   — 33 ítems
-- ============================================================================


-- ============================================================================
-- § 1  FUNCIÓN: erp_seed_tenant_default_profiles
-- ----------------------------------------------------------------------------
-- Crea perfiles Facturador / Bodeguero / Contador para un tenant.
-- Uso principal: DefaultProfileSeeder.cs (EF Core) llama a SeedForTenantAsync()
-- en cada onboarding. Esta función existe como herramienta de emergencia/psql.
--
-- Claves de permisos: inglés (sales.* / inventory.* / purchases.* / finance.*),
-- alineadas con Permissions.cs tras el refactor de renombrado de módulos.
-- ============================================================================

CREATE OR REPLACE FUNCTION erp_seed_tenant_default_profiles(
    p_tenant_id UUID,
    p_actor_id  UUID
)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    v_facturador_id UUID;
    v_bodeguero_id  UUID;
    v_contador_id   UUID;
BEGIN

    -- 1a. Crear perfiles (idempotente) ----------------------------------------
    INSERT INTO access_profiles
        (id, tenant_id, name, description, is_active, created_at, created_by)
    VALUES
        (gen_random_uuid(), p_tenant_id, 'Facturador', 'Billing operator — can create and void invoices.',  TRUE, NOW(), p_actor_id),
        (gen_random_uuid(), p_tenant_id, 'Bodeguero',  'Warehouse operator — manages stock and transfers.', TRUE, NOW(), p_actor_id),
        (gen_random_uuid(), p_tenant_id, 'Contador',   'Accountant — read-only access to accounting data.', TRUE, NOW(), p_actor_id)
    ON CONFLICT (tenant_id, name) DO NOTHING;

    SELECT id INTO v_facturador_id FROM access_profiles WHERE tenant_id = p_tenant_id AND name = 'Facturador';
    SELECT id INTO v_bodeguero_id  FROM access_profiles WHERE tenant_id = p_tenant_id AND name = 'Bodeguero';
    SELECT id INTO v_contador_id   FROM access_profiles WHERE tenant_id = p_tenant_id AND name = 'Contador';

    -- 1b. Permisos Facturador -------------------------------------------------
    INSERT INTO access_profile_permissions
        (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
    SELECT gen_random_uuid(), p_tenant_id, v_facturador_id, key, TRUE, NOW(), p_actor_id
    FROM unnest(ARRAY[
        'sales.invoices.view',
        'sales.invoices.create',
        'sales.invoices.update',
        'sales.invoices.void',
        'sales.credit-notes.view',
        'sales.credit-notes.create',
        'sales.customers.view',
        'sales.customers.create',
        'sales.customers.update',
        'inventory.products.view'
    ]) AS t(key)
    ON CONFLICT (tenant_id, profile_id, permission_key) DO NOTHING;

    -- 1c. Permisos Bodeguero --------------------------------------------------
    INSERT INTO access_profile_permissions
        (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
    SELECT gen_random_uuid(), p_tenant_id, v_bodeguero_id, key, TRUE, NOW(), p_actor_id
    FROM unnest(ARRAY[
        'inventory.products.view',
        'inventory.warehouses.view',
        'inventory.transfers.view',
        'inventory.transfers.create',
        'inventory.adjustments.view',
        'inventory.adjustments.create',
        'purchases.orders.view'
    ]) AS t(key)
    ON CONFLICT (tenant_id, profile_id, permission_key) DO NOTHING;

    -- 1d. Permisos Contador ---------------------------------------------------
    INSERT INTO access_profile_permissions
        (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
    SELECT gen_random_uuid(), p_tenant_id, v_contador_id, key, TRUE, NOW(), p_actor_id
    FROM unnest(ARRAY[
        'finance.config.view',
        'finance.accounts.view',
        'finance.accounts.create',
        'finance.accounts.edit',
        'finance.journal.view',
        'sales.invoices.view',
        'purchases.orders.view'
    ]) AS t(key)
    ON CONFLICT (tenant_id, profile_id, permission_key) DO NOTHING;

END;
$$;

COMMENT ON FUNCTION erp_seed_tenant_default_profiles(UUID, UUID) IS
    'Seeds Facturador / Bodeguero / Contador access profiles for a given tenant. Idempotent. Emergency/psql tool — primary path: DefaultProfileSeeder.cs.';


-- ============================================================================
-- § 2  GRUPOS DE NAVEGACIÓN GLOBAL  (ui_nav_groups)
-- ----------------------------------------------------------------------------
-- Fallback global del menú de navegación: 9 grupos ordenados por sort_order.
-- Los planes SaaS pueden sobrescribir esta estructura con menu_config_json
-- personalizado (gestionado desde SuperAdmin → Planes → Menu Builder).
-- ============================================================================

INSERT INTO ui_nav_groups ("Id", code, icon, label_key, sort_order, module_key, require_superadmin_panel, is_active)
VALUES
  (gen_random_uuid(), 'sales',     '🧾', 'app.nav.group.sales',     10, 'sales',     false, true),
  (gen_random_uuid(), 'purchases', '🛒', 'app.nav.group.purchases',  20, 'purchases', false, true),
  (gen_random_uuid(), 'inventory', '📦', 'app.nav.group.inventory',  30, 'inventory', false, true),
  (gen_random_uuid(), 'logistics', '🚚', 'app.nav.group.logistics',  40, 'logistics', false, true),
  (gen_random_uuid(), 'cash',      '💳', 'app.nav.group.cash',       50, 'cash',      false, true),
  (gen_random_uuid(), 'finance',   '📒', 'app.nav.group.finance',    60, 'finance',   false, true),
  (gen_random_uuid(), 'expenses',  '💸', 'app.nav.group.expenses',   70, 'expenses',  false, true),
  (gen_random_uuid(), 'settings',  '⚙',  'app.nav.group.settings',  80, 'settings',  false, true),
  (gen_random_uuid(), 'admin',     '🛡️', 'app.nav.group.admin',      90, 'admin',     false, true)
ON CONFLICT (code) DO NOTHING;


-- ============================================================================
-- § 3  ÍTEMS DE NAVEGACIÓN GLOBAL  (ui_nav_items)
-- ----------------------------------------------------------------------------
-- 33 ítems distribuidos en los 9 grupos. Cada ítem referencia un group_id
-- resuelto por código para evitar dependencias de UUID hardcodeados.
-- ============================================================================

-- ── Ventas (4 ítems) ─────────────────────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id", v.route_path, v.label_key, v.display_label, v.sort_order::int, v.module_key, v.permission_key, true
FROM ui_nav_groups g
CROSS JOIN (VALUES
  ('/sales/invoices',             'app.nav.sales.invoices',                  'Facturas',              10, 'sales', 'sales.invoices.view'),
  ('/sales/customers',            'app.nav.catalog.customers',               'Clientes',              20, 'sales', 'sales.customers.view'),
  ('/sales/credit-notes',         'app.nav.item.sales.credit-notes',         'Notas de crédito',      30, 'sales', 'sales.credit-notes.view'),
  ('/sales/withholding-received', 'app.nav.item.sales.withholding-received', 'Retenciones recibidas', 40, 'sales', 'sales.withholding-received.view')
) AS v(route_path, label_key, display_label, sort_order, module_key, permission_key)
WHERE g.code = 'sales'
  AND NOT EXISTS (SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = v.route_path);

-- ── Compras (5 ítems) ────────────────────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id", v.route_path, v.label_key, v.display_label, v.sort_order::int, v.module_key, v.permission_key, true
FROM ui_nav_groups g
CROSS JOIN (VALUES
  ('/purchases/invoices',           'app.nav.item.purchases.invoices',          'Facturas de compra',      10, 'purchases', 'purchases.invoices.view'),
  ('/purchases/suppliers',          'app.nav.item.purchases.suppliers',         'Proveedores',             20, 'purchases', 'purchases.suppliers.view'),
  ('/purchases/orders',             'app.nav.item.purchases.orders',            'Órdenes de compra',       30, 'purchases', 'purchases.orders.view'),
  ('/purchases/credit-notes',       'app.nav.item.purchases.credit-notes',      'Notas crédito proveedor', 40, 'purchases', 'purchases.credit-notes.view'),
  ('/purchases/withholding-issued', 'app.nav.item.purchases.withholding-issued','Retenciones emitidas',    50, 'purchases', 'purchases.withholding-issued.view')
) AS v(route_path, label_key, display_label, sort_order, module_key, permission_key)
WHERE g.code = 'purchases'
  AND NOT EXISTS (SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = v.route_path);

-- ── Inventario (11 ítems) ────────────────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id", v.route_path, v.label_key, v.display_label, v.sort_order::int, v.module_key, v.permission_key, true
FROM ui_nav_groups g
CROSS JOIN (VALUES
  ('/inventory/products',          'app.nav.item.inventory.products',   'Productos',           10,  'inventory', 'inventory.products.view'),
  ('/inventory/stock',             'app.nav.item.inventory.stock',      'Stock por bodega',    20,  'inventory', 'inventory.stock.view'),
  ('/inventory/warehouses',        'app.nav.item.inventory.warehouses', 'Bodegas',             30,  'inventory', 'inventory.warehouses.view'),
  ('/inventory/kardex',            'app.nav.item.inventory.kardex',     'Kardex',              40,  'inventory', 'inventory.kardex.view'),
  ('/inventory/adjustments',       'app.nav.catalog.ajustes',           'Ajustes inventario',  50,  'inventory', 'inventory.adjustments.view'),
  ('/inventory/transfers',         'app.nav.catalog.transferencias',    'Transferencias',      60,  'inventory', 'inventory.transfers.view'),
  ('/inventory/brands',            'app.nav.catalog.brands',            'Marcas',              70,  'inventory', 'inventory.brands.view'),
  ('/inventory/tariffs',           'app.nav.catalog.tariffs',           'Tarifas',             80,  'inventory', 'inventory.tariffs.view'),
  ('/inventory/units',             'app.nav.catalog.units',             'Unidades de medida',  90,  'inventory', 'inventory.units.view'),
  ('/inventory/catalog-structure', 'app.nav.catalog.structure',         'Estructura catálogo', 100, 'inventory', 'inventory.catalog.view'),
  ('/inventory/product-types',     'app.nav.catalog.productTypes',      'Tipos de producto',   110, 'inventory', 'inventory.product-types.view')
) AS v(route_path, label_key, display_label, sort_order, module_key, permission_key)
WHERE g.code = 'inventory'
  AND NOT EXISTS (SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = v.route_path);

-- ── Logística (1 ítem) ───────────────────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id", v.route_path, v.label_key, v.display_label, v.sort_order::int, v.module_key, v.permission_key, true
FROM ui_nav_groups g
CROSS JOIN (VALUES
  ('/logistics/carriers', 'app.nav.item.logistics.carriers', 'Transportistas', 10, 'logistics', 'logistics.carriers.view')
) AS v(route_path, label_key, display_label, sort_order, module_key, permission_key)
WHERE g.code = 'logistics'
  AND NOT EXISTS (SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = v.route_path);

-- ── Caja y Bancos (1 ítem) ───────────────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id", v.route_path, v.label_key, v.display_label, v.sort_order::int, v.module_key, v.permission_key, true
FROM ui_nav_groups g
CROSS JOIN (VALUES
  ('/cash/bank', 'app.nav.item.cash.bank', 'Caja y bancos', 10, 'cash', 'cash.bank.view')
) AS v(route_path, label_key, display_label, sort_order, module_key, permission_key)
WHERE g.code = 'cash'
  AND NOT EXISTS (SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = v.route_path);

-- ── Finanzas (2 ítems) ───────────────────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id", v.route_path, v.label_key, v.display_label, v.sort_order::int, v.module_key, v.permission_key, true
FROM ui_nav_groups g
CROSS JOIN (VALUES
  ('/finance/accounts', 'app.nav.item.finance.accounts', 'Contabilidad',     10, 'finance', 'finance.accounts.view'),
  ('/finance/config',   'app.nav.item.finance.config',   'Config. contable', 20, 'finance', 'finance.config.view')
) AS v(route_path, label_key, display_label, sort_order, module_key, permission_key)
WHERE g.code = 'finance'
  AND NOT EXISTS (SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = v.route_path);

-- ── Gastos (1 ítem) ──────────────────────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id", v.route_path, v.label_key, v.display_label, v.sort_order::int, v.module_key, v.permission_key, true
FROM ui_nav_groups g
CROSS JOIN (VALUES
  ('/expenses', 'app.nav.item.expenses.invoices', 'Gastos', 10, 'expenses', 'expenses.invoices.view')
) AS v(route_path, label_key, display_label, sort_order, module_key, permission_key)
WHERE g.code = 'expenses'
  AND NOT EXISTS (SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = v.route_path);

-- ── Configuración (5 ítems) ──────────────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id", v.route_path, v.label_key, v.display_label, v.sort_order::int, v.module_key, v.permission_key, true
FROM ui_nav_groups g
CROSS JOIN (VALUES
  ('/settings/company',   'app.nav.item.settings.company',   'Datos de Empresa',   10, 'settings', 'settings.company.view'),
  ('/settings/sri',       'app.nav.item.settings.sri',       'Configuración SRI',  20, 'settings', 'settings.sri.view'),
  ('/settings/ride',      'app.nav.item.settings.ride',      'Configuración RIDE', 30, 'settings', 'settings.ride.view'),
  ('/settings/branches',  'app.nav.item.settings.branches',  'Sucursales',         40, 'settings', 'settings.branches.view'),
  ('/settings/geography', 'app.nav.item.settings.geography', 'Geografía',          50, 'settings', 'settings.geography.view')
) AS v(route_path, label_key, display_label, sort_order, module_key, permission_key)
WHERE g.code = 'settings'
  AND NOT EXISTS (SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = v.route_path);

-- ── Administración (3 ítems) ─────────────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id", v.route_path, v.label_key, v.display_label, v.sort_order::int, v.module_key, v.permission_key, true
FROM ui_nav_groups g
CROSS JOIN (VALUES
  ('/admin/roles',    'app.nav.item.admin.roles',    'Perfiles (Roles)', 10, 'admin', 'admin.roles.view'),
  ('/admin/users',    'app.nav.item.admin.users',    'Acceso usuarios',  20, 'admin', 'admin.users.view'),
  ('/admin/activity', 'app.nav.item.admin.activity', 'Actividad',        30, 'admin', 'admin.activity.view')
) AS v(route_path, label_key, display_label, sort_order, module_key, permission_key)
WHERE g.code = 'admin'
  AND NOT EXISTS (SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = v.route_path);
