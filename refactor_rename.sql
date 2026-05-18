BEGIN;

-- ═══════════════════════════════════════════════════════════════
-- 1. ui_nav_groups — actualizar codes
-- ═══════════════════════════════════════════════════════════════
UPDATE ui_nav_groups SET code = 'finance'        WHERE code = 'contabilidad';
UPDATE ui_nav_groups SET code = 'sales'          WHERE code = 'ventas';
UPDATE ui_nav_groups SET code = 'purchases'      WHERE code = 'compras';
UPDATE ui_nav_groups SET code = 'expenses'       WHERE code = 'gastos';
UPDATE ui_nav_groups SET code = 'inventory'      WHERE code = 'inventario';
UPDATE ui_nav_groups SET code = 'inventory'      WHERE code = 'catalog';
UPDATE ui_nav_groups SET code = 'logistics'      WHERE code = 'logistica';
UPDATE ui_nav_groups SET code = 'cash'           WHERE code = 'caja';
UPDATE ui_nav_groups SET code = 'settings'       WHERE code = 'configuracion';
UPDATE ui_nav_groups SET code = 'administration' WHERE code = 'acceso';
UPDATE ui_nav_groups SET code = 'administration' WHERE code = 'seguridad';

-- ═══════════════════════════════════════════════════════════════
-- 2. ui_nav_items — rutas y permisos
-- ═══════════════════════════════════════════════════════════════
WITH route_map(old_route, new_route, new_perm) AS (VALUES
  ('/contabilidad',                 '/finance/accounts',             'finance.accounts.view'),
  ('/accounting',                   '/finance/accounts',             'finance.accounts.view'),
  ('/contabilidad/configuracion',   '/finance/config',               'finance.config.view'),
  ('/ventas',                       '/sales/invoices',               'sales.invoices.view'),
  ('/ventas/facturas',              '/sales/invoices',               'sales.invoices.view'),
  ('/ventas/customers',             '/sales/customers',              'sales.customers.view'),
  ('/ventas/clientes',              '/sales/customers',              'sales.customers.view'),
  ('/ventas/notas',                 '/sales/credit-notes',           'sales.credit-notes.view'),
  ('/ventas/retenciones-recibidas', '/sales/withholding-received',   'sales.withholding-received.view'),
  ('/compras',                      '/purchases/invoices',           'purchases.invoices.view'),
  ('/compras/facturas',             '/purchases/invoices',           'purchases.invoices.view'),
  ('/compras/proveedores',          '/purchases/suppliers',          'purchases.suppliers.view'),
  ('/compras/ordenes',              '/purchases/orders',             'purchases.orders.view'),
  ('/compras/notas-proveedor',      '/purchases/credit-notes',       'purchases.credit-notes.view'),
  ('/compras/retenciones',          '/purchases/withholding-issued', 'purchases.withholding-issued.view'),
  ('/gastos',                       '/expenses',                     'expenses.invoices.view'),
  ('/products',                     '/inventory/products',           'inventory.products.view'),
  ('/inventario/products',          '/inventory/products',           'inventory.products.view'),
  ('/inventario/stock',             '/inventory/stock',              'inventory.stock.view'),
  ('/inventario/bodegas',           '/inventory/warehouses',         'inventory.warehouses.view'),
  ('/inventario/kardex',            '/inventory/kardex',             'inventory.kardex.view'),
  ('/inventario/ajustes',           '/inventory/adjustments',        'inventory.adjustments.view'),
  ('/inventario/transferencias',    '/inventory/transfers',          'inventory.transfers.view'),
  ('/inventario/brands',            '/inventory/brands',             'inventory.brands.view'),
  ('/inventario/tariffs',           '/inventory/tariffs',            'inventory.tariffs.view'),
  ('/inventario/units',             '/inventory/units',              'inventory.units.view'),
  ('/inventario/structure',         '/inventory/catalog-structure',  'inventory.catalog.view'),
  ('/inventario/product-types',     '/inventory/product-types',      'inventory.product-types.view'),
  ('/logistica/transportistas',     '/logistics/carriers',           'logistics.carriers.view'),
  ('/caja',                         '/cash/bank',                    'cash.bank.view'),
  ('/configuracion/empresa',        '/settings/company',             'settings.company.view'),
  ('/configuracion/sri',            '/settings/sri',                 'settings.sri.view'),
  ('/configuracion/facturacion',    '/settings/ride',                'settings.ride.view'),
  ('/saas/branches',                '/settings/branches',            'settings.branches.view'),
  ('/configuracion/sucursales',     '/settings/branches',            'settings.branches.view'),
  ('/configuracion/geografia',      '/settings/geography',           'settings.geography.view'),
  ('/profiles',                     '/admin/roles',                  'admin.roles.view'),
  ('/access',                       '/admin/users',                  'admin.users.view'),
  ('/actividad',                    '/admin/activity',               'admin.activity.view'),
  ('/security',                     '/admin/security',               'admin.security.view')
)
UPDATE ui_nav_items AS t
SET route_path = m.new_route, permission_key = m.new_perm
FROM route_map m WHERE t.route_path = m.old_route;

-- ═══════════════════════════════════════════════════════════════
-- 3. permission — renombrar códigos
-- ═══════════════════════════════════════════════════════════════
WITH perm_map(old_key, new_key) AS (VALUES
  ('accounting.accounts.view',   'finance.accounts.view'),
  ('accounting.accounts.create', 'finance.accounts.create'),
  ('accounting.accounts.edit',   'finance.accounts.edit'),
  ('accounting.config.view',     'finance.config.view'),
  ('accounting.config.edit',     'finance.config.edit'),
  ('accounting.journal.view',    'finance.journal.view'),
  ('ventas.invoice.view',        'sales.invoices.view'),
  ('ventas.invoice.create',      'sales.invoices.create'),
  ('ventas.invoice.update',      'sales.invoices.update'),
  ('ventas.invoice.void',        'sales.invoices.void'),
  ('ventas.credit-note.view',    'sales.credit-notes.view'),
  ('ventas.credit-note.create',  'sales.credit-notes.create'),
  ('ventas.credit-note.void',    'sales.credit-notes.void'),
  ('ventas.debit-note.view',     'sales.debit-notes.view'),
  ('ventas.debit-note.create',   'sales.debit-notes.create'),
  ('ventas.customers.view',      'sales.customers.view'),
  ('ventas.customers.create',    'sales.customers.create'),
  ('ventas.customers.update',    'sales.customers.update'),
  ('ventas.customers.delete',    'sales.customers.delete'),
  ('compras.orders.view',        'purchases.orders.view'),
  ('compras.orders.create',      'purchases.orders.create'),
  ('compras.orders.approve',     'purchases.orders.approve'),
  ('compras.orders.cancel',      'purchases.orders.cancel'),
  ('compras.proveedores.view',   'purchases.suppliers.view'),
  ('compras.proveedores.create', 'purchases.suppliers.create'),
  ('compras.proveedores.update', 'purchases.suppliers.update'),
  ('compras.proveedores.delete', 'purchases.suppliers.delete'),
  ('inventario.products.view',   'inventory.products.view'),
  ('inventario.products.create', 'inventory.products.create'),
  ('inventario.products.update', 'inventory.products.update'),
  ('inventario.products.delete', 'inventory.products.delete'),
  ('inventario.brands.view',     'inventory.brands.view'),
  ('inventario.brands.create',   'inventory.brands.create'),
  ('inventario.brands.update',   'inventory.brands.update'),
  ('inventario.brands.delete',   'inventory.brands.delete'),
  ('inventario.warehouses.view',    'inventory.warehouses.view'),
  ('inventario.warehouses.create',  'inventory.warehouses.create'),
  ('inventario.warehouses.update',  'inventory.warehouses.update'),
  ('inventario.transfers.view',     'inventory.transfers.view'),
  ('inventario.transfers.create',   'inventory.transfers.create'),
  ('inventario.transfers.approve',  'inventory.transfers.approve'),
  ('inventario.adjustments.view',   'inventory.adjustments.view'),
  ('inventario.adjustments.create', 'inventory.adjustments.create'),
  ('inventario.adjustments.approve','inventory.adjustments.approve'),
  ('logistica.carriers.view',    'logistics.carriers.view'),
  ('logistica.carriers.create',  'logistics.carriers.create'),
  ('logistica.carriers.update',  'logistics.carriers.update'),
  ('logistica.carriers.delete',  'logistics.carriers.delete'),
  ('access.profiles.view',       'admin.roles.view'),
  ('access.memberships.view',    'admin.users.view')
)
UPDATE permission AS p SET code = m.new_key
FROM perm_map m WHERE p.code = m.old_key;

-- ═══════════════════════════════════════════════════════════════
-- 4. role_permission — actualizar referencias
-- ═══════════════════════════════════════════════════════════════
WITH perm_map(old_key, new_key) AS (VALUES
  ('accounting.accounts.view',   'finance.accounts.view'),
  ('accounting.accounts.create', 'finance.accounts.create'),
  ('accounting.accounts.edit',   'finance.accounts.edit'),
  ('accounting.config.view',     'finance.config.view'),
  ('accounting.config.edit',     'finance.config.edit'),
  ('accounting.journal.view',    'finance.journal.view'),
  ('ventas.invoice.view',        'sales.invoices.view'),
  ('ventas.invoice.create',      'sales.invoices.create'),
  ('ventas.invoice.update',      'sales.invoices.update'),
  ('ventas.invoice.void',        'sales.invoices.void'),
  ('ventas.credit-note.view',    'sales.credit-notes.view'),
  ('ventas.credit-note.create',  'sales.credit-notes.create'),
  ('ventas.credit-note.void',    'sales.credit-notes.void'),
  ('ventas.debit-note.view',     'sales.debit-notes.view'),
  ('ventas.debit-note.create',   'sales.debit-notes.create'),
  ('ventas.customers.view',      'sales.customers.view'),
  ('ventas.customers.create',    'sales.customers.create'),
  ('ventas.customers.update',    'sales.customers.update'),
  ('ventas.customers.delete',    'sales.customers.delete'),
  ('compras.orders.view',        'purchases.orders.view'),
  ('compras.orders.create',      'purchases.orders.create'),
  ('compras.orders.approve',     'purchases.orders.approve'),
  ('compras.orders.cancel',      'purchases.orders.cancel'),
  ('compras.proveedores.view',   'purchases.suppliers.view'),
  ('compras.proveedores.create', 'purchases.suppliers.create'),
  ('compras.proveedores.update', 'purchases.suppliers.update'),
  ('compras.proveedores.delete', 'purchases.suppliers.delete'),
  ('inventario.products.view',   'inventory.products.view'),
  ('inventario.products.create', 'inventory.products.create'),
  ('inventario.products.update', 'inventory.products.update'),
  ('inventario.products.delete', 'inventory.products.delete'),
  ('inventario.brands.view',     'inventory.brands.view'),
  ('inventario.brands.create',   'inventory.brands.create'),
  ('inventario.brands.update',   'inventory.brands.update'),
  ('inventario.brands.delete',   'inventory.brands.delete'),
  ('inventario.warehouses.view',    'inventory.warehouses.view'),
  ('inventario.warehouses.create',  'inventory.warehouses.create'),
  ('inventario.warehouses.update',  'inventory.warehouses.update'),
  ('inventario.transfers.view',     'inventory.transfers.view'),
  ('inventario.transfers.create',   'inventory.transfers.create'),
  ('inventario.transfers.approve',  'inventory.transfers.approve'),
  ('inventario.adjustments.view',   'inventory.adjustments.view'),
  ('inventario.adjustments.create', 'inventory.adjustments.create'),
  ('inventario.adjustments.approve','inventory.adjustments.approve'),
  ('logistica.carriers.view',    'logistics.carriers.view'),
  ('logistica.carriers.create',  'logistics.carriers.create'),
  ('logistica.carriers.update',  'logistics.carriers.update'),
  ('logistica.carriers.delete',  'logistics.carriers.delete'),
  ('access.profiles.view',       'admin.roles.view'),
  ('access.memberships.view',    'admin.users.view')
)
UPDATE role_permission AS rp SET permission_code = m.new_key
FROM perm_map m WHERE rp.permission_code = m.old_key;

-- ═══════════════════════════════════════════════════════════════
-- 5. app_features — actualizar path y permission
-- ═══════════════════════════════════════════════════════════════
WITH feat_map(old_path, new_path, new_perm) AS (VALUES
  ('/contabilidad',                '/finance/accounts',            'perm:finance.accounts.view'),
  ('/accounting',                  '/finance/accounts',            'perm:finance.accounts.view'),
  ('/contabilidad/configuracion',  '/finance/config',              'perm:finance.config.view'),
  ('/ventas',                      '/sales/invoices',              'perm:sales.invoices.view'),
  ('/ventas/facturas',             '/sales/invoices',              'perm:sales.invoices.view'),
  ('/ventas/clientes',             '/sales/customers',             'perm:sales.customers.view'),
  ('/ventas/customers',            '/sales/customers',             'perm:sales.customers.view'),
  ('/ventas/notas',                '/sales/credit-notes',          'perm:sales.credit-notes.view'),
  ('/ventas/retenciones-recibidas','/sales/withholding-received',  'perm:sales.withholding-received.view'),
  ('/compras',                     '/purchases/invoices',          'perm:purchases.invoices.view'),
  ('/compras/facturas',            '/purchases/invoices',          'perm:purchases.invoices.view'),
  ('/compras/proveedores',         '/purchases/suppliers',         'perm:purchases.suppliers.view'),
  ('/compras/ordenes',             '/purchases/orders',            'perm:purchases.orders.view'),
  ('/compras/notas-proveedor',     '/purchases/credit-notes',      'perm:purchases.credit-notes.view'),
  ('/compras/retenciones',         '/purchases/withholding-issued','perm:purchases.withholding-issued.view'),
  ('/gastos',                      '/expenses',                    'perm:expenses.invoices.view'),
  ('/products',                    '/inventory/products',          'perm:inventory.products.view'),
  ('/inventario/products',         '/inventory/products',          'perm:inventory.products.view'),
  ('/inventario/stock',            '/inventory/stock',             'perm:inventory.stock.view'),
  ('/inventario/bodegas',          '/inventory/warehouses',        'perm:inventory.warehouses.view'),
  ('/inventario/kardex',           '/inventory/kardex',            'perm:inventory.kardex.view'),
  ('/inventario/ajustes',          '/inventory/adjustments',       'perm:inventory.adjustments.view'),
  ('/inventario/transferencias',   '/inventory/transfers',         'perm:inventory.transfers.view'),
  ('/inventario/brands',           '/inventory/brands',            'perm:inventory.brands.view'),
  ('/inventario/tariffs',          '/inventory/tariffs',           'perm:inventory.tariffs.view'),
  ('/inventario/units',            '/inventory/units',             'perm:inventory.units.view'),
  ('/inventario/structure',        '/inventory/catalog-structure', 'perm:inventory.catalog.view'),
  ('/inventario/product-types',    '/inventory/product-types',     'perm:inventory.product-types.view'),
  ('/logistica/transportistas',    '/logistics/carriers',          'perm:logistics.carriers.view'),
  ('/caja',                        '/cash/bank',                   'perm:cash.bank.view'),
  ('/configuracion/empresa',       '/settings/company',            'perm:settings.company.view'),
  ('/configuracion/sri',           '/settings/sri',                'perm:settings.sri.view'),
  ('/configuracion/facturacion',   '/settings/ride',               'perm:settings.ride.view'),
  ('/saas/branches',               '/settings/branches',           'perm:settings.branches.view'),
  ('/configuracion/sucursales',    '/settings/branches',           'perm:settings.branches.view'),
  ('/configuracion/geografia',     '/settings/geography',          'perm:settings.geography.view'),
  ('/profiles',                    '/admin/roles',                 'perm:admin.roles.view'),
  ('/access',                      '/admin/users',                 'perm:admin.users.view'),
  ('/actividad',                   '/admin/activity',              'perm:admin.activity.view')
)
UPDATE app_features AS f
SET path = m.new_path, permission = m.new_perm
FROM feat_map m WHERE f.path = m.old_path;

-- ═══════════════════════════════════════════════════════════════
-- 6. saas_plans MenuConfigJson — reemplazar rutas
-- ═══════════════════════════════════════════════════════════════
UPDATE saas_plans SET menu_config_json =
  replace(replace(replace(replace(replace(replace(replace(replace(
  replace(replace(replace(replace(replace(replace(replace(replace(
  replace(replace(replace(replace(replace(replace(replace(replace(
  replace(replace(replace(replace(replace(replace(replace(replace(
  replace(replace(replace(replace(menu_config_json,
    '"/ventas/facturas"',            '"/sales/invoices"'),
    '"/ventas/customers"',           '"/sales/customers"'),
    '"/ventas/notas"',               '"/sales/credit-notes"'),
    '"/ventas/retenciones-recibidas"','"/sales/withholding-received"'),
    '"/compras/facturas"',           '"/purchases/invoices"'),
    '"/compras/proveedores"',        '"/purchases/suppliers"'),
    '"/compras/ordenes"',            '"/purchases/orders"'),
    '"/compras/notas-proveedor"',    '"/purchases/credit-notes"'),
    '"/compras/retenciones"',        '"/purchases/withholding-issued"'),
    '"/gastos"',                     '"/expenses"'),
    '"/inventario/ajustes"',         '"/inventory/adjustments"'),
    '"/inventario/transferencias"',  '"/inventory/transfers"'),
    '"/inventario/brands"',          '"/inventory/brands"'),
    '"/inventario/tariffs"',         '"/inventory/tariffs"'),
    '"/inventario/units"',           '"/inventory/units"'),
    '"/inventario/structure"',       '"/inventory/catalog-structure"'),
    '"/inventario/product-types"',   '"/inventory/product-types"'),
    '"/inventario/products"',        '"/inventory/products"'),
    '"/inventario/bodegas"',         '"/inventory/warehouses"'),
    '"/logistica/transportistas"',   '"/logistics/carriers"'),
    '"/configuracion/empresa"',      '"/settings/company"'),
    '"/configuracion/sri"',          '"/settings/sri"'),
    '"/configuracion/facturacion"',  '"/settings/ride"'),
    '"/saas/branches"',              '"/settings/branches"'),
    '"/profiles"',                   '"/admin/roles"'),
    '"/access"',                     '"/admin/users"'),
    '"/actividad"',                  '"/admin/activity"'),
    '"/accounting"',                 '"/finance/accounts"'),
    '"/contabilidad"',               '"/finance/accounts"'),
    '"perm:ventas.facturas.view"',   '"perm:sales.invoices.view"'),
    '"perm:ventas.customers.view"',  '"perm:sales.customers.view"'),
    '"perm:compras.facturas.view"',  '"perm:purchases.invoices.view"'),
    '"perm:compras.proveedores.view"','"perm:purchases.suppliers.view"'),
    '"perm:inventario.products.view"','"perm:inventory.products.view"'),
    '"perm:logistica.carriers.view"','"/perm:logistics.carriers.view"'),
    '"perm:access.profiles.view"',   '"perm:admin.roles.view"'),
    '"perm:access.memberships.view"','"perm:admin.users.view"')
WHERE menu_config_json IS NOT NULL;

COMMIT;
