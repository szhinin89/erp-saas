BEGIN;

-- ═══════════════════════════════════════════════════════════════
-- 1. ui_nav_groups — solo existe 'configuracion'
-- ═══════════════════════════════════════════════════════════════
UPDATE ui_nav_groups SET code = 'settings' WHERE code = 'configuracion';

-- ═══════════════════════════════════════════════════════════════
-- 2. ui_nav_items — 3 filas actuales
-- ═══════════════════════════════════════════════════════════════
WITH route_map(old_route, new_route, new_perm) AS (VALUES
  ('/configuracion/empresa',     '/settings/company', 'settings.company.view'),
  ('/configuracion/sri',         '/settings/sri',     'settings.sri.view'),
  ('/configuracion/facturacion', '/settings/ride',    'settings.ride.view'),
  ('/saas/branches',             '/settings/branches','settings.branches.view'),
  ('/profiles',                  '/admin/roles',      'admin.roles.view'),
  ('/access',                    '/admin/users',      'admin.users.view')
)
UPDATE ui_nav_items t
SET route_path = m.new_route, permission_key = m.new_perm
FROM route_map m WHERE t.route_path = m.old_route;

-- ═══════════════════════════════════════════════════════════════
-- 3. app_features — actualizar todos los paths y permisos
-- ═══════════════════════════════════════════════════════════════
WITH feat_map(old_path, new_path, new_perm) AS (VALUES
  ('/contabilidad',                '/finance/accounts',            'perm:finance.accounts.view'),
  ('/contabilidad/configuracion',  '/finance/config',              'perm:finance.config.view'),
  ('/ventas',                      '/sales/invoices',              'perm:sales.invoices.view'),
  ('/ventas/notas',                '/sales/credit-notes',          'perm:sales.credit-notes.view'),
  ('/ventas/clientes',             '/sales/customers',             'perm:sales.customers.view'),
  ('/ventas/retenciones-recibidas','/sales/withholding-received',  'perm:sales.withholding-received.view'),
  ('/compras',                     '/purchases/invoices',          'perm:purchases.invoices.view'),
  ('/compras/proveedores',         '/purchases/suppliers',         'perm:purchases.suppliers.view'),
  ('/compras/ordenes',             '/purchases/orders',            'perm:purchases.orders.view'),
  ('/compras/notas-proveedor',     '/purchases/credit-notes',      'perm:purchases.credit-notes.view'),
  ('/compras/retenciones',         '/purchases/withholding-issued','perm:purchases.withholding-issued.view'),
  ('/gastos',                      '/expenses',                    'perm:expenses.invoices.view'),
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
  ('/configuracion/sucursales',    '/settings/branches',           'perm:settings.branches.view'),
  ('/configuracion/geografia',     '/settings/geography',          'perm:settings.geography.view'),
  ('/profiles',                    '/admin/roles',                 'perm:admin.roles.view'),
  ('/access',                      '/admin/users',                 'perm:admin.users.view'),
  ('/actividad',                   '/admin/activity',              'perm:admin.activity.view')
)
UPDATE app_features f
SET path = m.new_path, permission = m.new_perm
FROM feat_map m WHERE f.path = m.old_path;

-- Actualizar también permissions padre que apuntan a rutas viejas
UPDATE app_features
SET permission = 'perm:finance.accounts.view'
WHERE permission = 'perm:accounting.accounts.view' AND path IS NULL;

UPDATE app_features
SET permission = 'perm:inventory.products.view'
WHERE permission = 'perm:inventario.products.view' AND path IS NULL;

UPDATE app_features
SET permission = 'perm:purchases.invoices.view'
WHERE permission = 'perm:compras.facturas.view' AND path IS NULL;

UPDATE app_features
SET permission = 'perm:sales.invoices.view'
WHERE permission = 'perm:ventas.facturas.view' AND path IS NULL;

-- Actualizar parentPermission en todos los features hijos
UPDATE app_features
SET parent_permission = 'perm:finance.accounts.view'
WHERE parent_permission = 'perm:accounting.accounts.view';

UPDATE app_features
SET parent_permission = 'perm:inventory.products.view'
WHERE parent_permission = 'perm:inventario.products.view';

UPDATE app_features
SET parent_permission = 'perm:purchases.invoices.view'
WHERE parent_permission = 'perm:compras.facturas.view';

UPDATE app_features
SET parent_permission = 'perm:sales.invoices.view'
WHERE parent_permission = 'perm:ventas.facturas.view';

-- ═══════════════════════════════════════════════════════════════
-- 4. access_profile_permissions — renombrar permission_key
--    (tabla vacía actualmente, queda preparada para cuando se pueble)
-- ═══════════════════════════════════════════════════════════════
WITH perm_map(old_key, new_key) AS (VALUES
  ('accounting.accounts.view',    'finance.accounts.view'),
  ('accounting.accounts.create',  'finance.accounts.create'),
  ('accounting.accounts.edit',    'finance.accounts.edit'),
  ('accounting.config.view',      'finance.config.view'),
  ('accounting.config.edit',      'finance.config.edit'),
  ('accounting.journal.view',     'finance.journal.view'),
  ('ventas.invoice.view',         'sales.invoices.view'),
  ('ventas.invoice.create',       'sales.invoices.create'),
  ('ventas.invoice.update',       'sales.invoices.update'),
  ('ventas.invoice.void',         'sales.invoices.void'),
  ('ventas.credit-note.view',     'sales.credit-notes.view'),
  ('ventas.credit-note.create',   'sales.credit-notes.create'),
  ('ventas.credit-note.void',     'sales.credit-notes.void'),
  ('ventas.debit-note.view',      'sales.debit-notes.view'),
  ('ventas.debit-note.create',    'sales.debit-notes.create'),
  ('ventas.customers.view',       'sales.customers.view'),
  ('ventas.customers.create',     'sales.customers.create'),
  ('ventas.customers.update',     'sales.customers.update'),
  ('ventas.customers.delete',     'sales.customers.delete'),
  ('compras.orders.view',         'purchases.orders.view'),
  ('compras.orders.create',       'purchases.orders.create'),
  ('compras.orders.approve',      'purchases.orders.approve'),
  ('compras.orders.cancel',       'purchases.orders.cancel'),
  ('compras.proveedores.view',    'purchases.suppliers.view'),
  ('compras.proveedores.create',  'purchases.suppliers.create'),
  ('compras.proveedores.update',  'purchases.suppliers.update'),
  ('compras.proveedores.delete',  'purchases.suppliers.delete'),
  ('inventario.products.view',    'inventory.products.view'),
  ('inventario.products.create',  'inventory.products.create'),
  ('inventario.products.update',  'inventory.products.update'),
  ('inventario.products.delete',  'inventory.products.delete'),
  ('inventario.brands.view',      'inventory.brands.view'),
  ('inventario.brands.create',    'inventory.brands.create'),
  ('inventario.brands.update',    'inventory.brands.update'),
  ('inventario.brands.delete',    'inventory.brands.delete'),
  ('inventario.warehouses.view',  'inventory.warehouses.view'),
  ('inventario.warehouses.create','inventory.warehouses.create'),
  ('inventario.warehouses.update','inventory.warehouses.update'),
  ('inventario.transfers.view',   'inventory.transfers.view'),
  ('inventario.transfers.create', 'inventory.transfers.create'),
  ('inventario.transfers.approve','inventory.transfers.approve'),
  ('inventario.adjustments.view', 'inventory.adjustments.view'),
  ('inventario.adjustments.create','inventory.adjustments.create'),
  ('inventario.adjustments.approve','inventory.adjustments.approve'),
  ('logistica.carriers.view',     'logistics.carriers.view'),
  ('logistica.carriers.create',   'logistics.carriers.create'),
  ('logistica.carriers.update',   'logistics.carriers.update'),
  ('logistica.carriers.delete',   'logistics.carriers.delete'),
  ('access.profiles.view',        'admin.roles.view'),
  ('access.memberships.view',     'admin.users.view')
)
UPDATE access_profile_permissions ap
SET permission_key = m.new_key
FROM perm_map m WHERE ap.permission_key = m.old_key;

-- ═══════════════════════════════════════════════════════════════
-- 5. saas_plans menu_config (jsonb) — reemplazar rutas en texto
-- ═══════════════════════════════════════════════════════════════
UPDATE saas_plans
SET menu_config = menu_config::text
  -- Rutas
  ::text::jsonb
WHERE menu_config IS NOT NULL
  AND menu_config::text LIKE '%ventas%';

-- Usando replace sobre el texto del JSONB
UPDATE saas_plans
SET menu_config = (
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
  regexp_replace(
    menu_config::text,
    '"/ventas/facturas"',             '"/sales/invoices"',           'g'),
    '"/ventas/customers"',            '"/sales/customers"',          'g'),
    '"/ventas/notas"',                '"/sales/credit-notes"',       'g'),
    '"/ventas/retenciones-recibidas"','"/sales/withholding-received"','g'),
    '"/compras/facturas"',            '"/purchases/invoices"',       'g'),
    '"/compras/proveedores"',         '"/purchases/suppliers"',      'g'),
    '"/compras/ordenes"',             '"/purchases/orders"',         'g'),
    '"/compras/notas-proveedor"',     '"/purchases/credit-notes"',   'g'),
    '"/compras/retenciones"',         '"/purchases/withholding-issued"','g'),
    '"/gastos"',                      '"/expenses"',                 'g'),
    '"/inventario/ajustes"',          '"/inventory/adjustments"',    'g'),
    '"/inventario/transferencias"',   '"/inventory/transfers"',      'g'),
    '"/inventario/brands"',           '"/inventory/brands"',         'g'),
    '"/inventario/tariffs"',          '"/inventory/tariffs"',        'g'),
    '"/inventario/units"',            '"/inventory/units"',          'g'),
    '"/inventario/structure"',        '"/inventory/catalog-structure"','g'),
    '"/inventario/product-types"',    '"/inventory/product-types"',  'g'),
    '"/inventario/products"',         '"/inventory/products"',       'g'),
    '"/inventario/bodegas"',          '"/inventory/warehouses"',     'g'),
    '"/logistica/transportistas"',    '"/logistics/carriers"',       'g'),
    '"/configuracion/empresa"',       '"/settings/company"',         'g'),
    '"/configuracion/sri"',           '"/settings/sri"',             'g'),
    '"/configuracion/facturacion"',   '"/settings/ride"',            'g'),
    '"/saas/branches"',               '"/settings/branches"',        'g'),
    '"/profiles"',                    '"/admin/roles"',              'g'),
    '"/access"',                      '"/admin/users"',              'g'),
    '"/actividad"',                   '"/admin/activity"',           'g'),
    '"/accounting"',                  '"/finance/accounts"',         'g')
  )::jsonb
WHERE menu_config IS NOT NULL;

COMMIT;
