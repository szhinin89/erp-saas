-- Seed: grupos y elementos del menú de navegación global (fallback).
-- Ejecutado una sola vez por checksum via InstallDataBootstrapService.
-- Idempotente: ON CONFLICT DO NOTHING en grupos; WHERE NOT EXISTS en ítems.
-- No usar BEGIN/COMMIT: el servicio ya ejecuta dentro de una transacción.

-- ── Grupos ────────────────────────────────────────────────────────────────
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

-- ── Ventas ────────────────────────────────────────────────────────────────
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

-- ── Compras ───────────────────────────────────────────────────────────────
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

-- ── Inventario ────────────────────────────────────────────────────────────
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

-- ── Logística ─────────────────────────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id", v.route_path, v.label_key, v.display_label, v.sort_order::int, v.module_key, v.permission_key, true
FROM ui_nav_groups g
CROSS JOIN (VALUES
  ('/logistics/carriers', 'app.nav.item.logistics.carriers', 'Transportistas', 10, 'logistics', 'logistics.carriers.view')
) AS v(route_path, label_key, display_label, sort_order, module_key, permission_key)
WHERE g.code = 'logistics'
  AND NOT EXISTS (SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = v.route_path);

-- ── Caja y Bancos ─────────────────────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id", v.route_path, v.label_key, v.display_label, v.sort_order::int, v.module_key, v.permission_key, true
FROM ui_nav_groups g
CROSS JOIN (VALUES
  ('/cash/bank', 'app.nav.item.cash.bank', 'Caja y bancos', 10, 'cash', 'cash.bank.view')
) AS v(route_path, label_key, display_label, sort_order, module_key, permission_key)
WHERE g.code = 'cash'
  AND NOT EXISTS (SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = v.route_path);

-- ── Finanzas ──────────────────────────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id", v.route_path, v.label_key, v.display_label, v.sort_order::int, v.module_key, v.permission_key, true
FROM ui_nav_groups g
CROSS JOIN (VALUES
  ('/finance/accounts', 'app.nav.item.finance.accounts', 'Contabilidad',     10, 'finance', 'finance.accounts.view'),
  ('/finance/config',   'app.nav.item.finance.config',   'Config. contable', 20, 'finance', 'finance.config.view')
) AS v(route_path, label_key, display_label, sort_order, module_key, permission_key)
WHERE g.code = 'finance'
  AND NOT EXISTS (SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = v.route_path);

-- ── Gastos ────────────────────────────────────────────────────────────────
INSERT INTO ui_nav_items ("Id", group_id, route_path, label_key, display_label, sort_order, module_key, permission_key, is_active)
SELECT gen_random_uuid(), g."Id", v.route_path, v.label_key, v.display_label, v.sort_order::int, v.module_key, v.permission_key, true
FROM ui_nav_groups g
CROSS JOIN (VALUES
  ('/expenses', 'app.nav.item.expenses.invoices', 'Gastos', 10, 'expenses', 'expenses.invoices.view')
) AS v(route_path, label_key, display_label, sort_order, module_key, permission_key)
WHERE g.code = 'expenses'
  AND NOT EXISTS (SELECT 1 FROM ui_nav_items i WHERE i.group_id = g."Id" AND i.route_path = v.route_path);

-- ── Configuración ─────────────────────────────────────────────────────────
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

-- ── Administración ────────────────────────────────────────────────────────
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
