-- =============================================================================
-- LEGACY ONLY — migracion pre-InitialEnterpriseBaseline
-- =============================================================================
-- Renombra rutas ES (/ventas, /configuracion) y permisos legacy a convencion
-- inglesa (/sales, /settings, sales.*, inventory.*, etc.).
--
-- NO ejecutar si la BD ya tiene migracion EF 20260521034018_InitialEnterpriseBaseline
-- ni InstallData 002_system_bootstrap.sql aplicado (menu ya en ingles).
--
-- Uso: psql $DATABASE_URL -v ON_ERROR_STOP=1 -f scripts/sql/legacy_pre_baseline_nav_permissions_rename.sql
--
-- Idempotente: cada UPDATE filtra por valor legacy; re-ejecutar es seguro.
-- Sustituye refactor_rename.sql / refactor_rename_v2.sql / refactor_rename_v3.sql
-- =============================================================================

-- 1. ui_nav_groups
UPDATE ui_nav_groups SET code = 'settings' WHERE code = 'configuracion';

-- ═══════════════════════════════════════════════════════════════
-- 2. ui_nav_items
-- ═══════════════════════════════════════════════════════════════
UPDATE ui_nav_items SET route_path = '/settings/company', permission_key = 'settings.company.view' WHERE route_path = '/configuracion/empresa';
UPDATE ui_nav_items SET route_path = '/settings/sri',     permission_key = 'settings.sri.view'     WHERE route_path = '/configuracion/sri';
UPDATE ui_nav_items SET route_path = '/settings/ride',    permission_key = 'settings.ride.view'    WHERE route_path = '/configuracion/facturacion';
UPDATE ui_nav_items SET route_path = '/settings/branches',permission_key = 'settings.branches.view'WHERE route_path = '/saas/branches';
UPDATE ui_nav_items SET route_path = '/admin/roles',      permission_key = 'admin.roles.view'      WHERE route_path = '/profiles';
UPDATE ui_nav_items SET route_path = '/admin/users',      permission_key = 'admin.users.view'      WHERE route_path = '/access';

-- ═══════════════════════════════════════════════════════════════
-- 3. app_features — permisos distintos por feature
-- ═══════════════════════════════════════════════════════════════
UPDATE app_features SET path = '/finance/accounts',          permission = 'perm:finance.accounts.view'             WHERE permission = 'perm:accounting.accounts.view';
UPDATE app_features SET path = '/finance/config',            permission = 'perm:finance.config.view'               WHERE permission = 'perm:accounting.config.view';
UPDATE app_features SET path = '/sales/invoices',            permission = 'perm:sales.invoices.view'               WHERE permission = 'perm:ventas.facturas.view';
UPDATE app_features SET path = '/sales/credit-notes',        permission = 'perm:sales.credit-notes.view'           WHERE permission = 'perm:ventas.notas.list';
UPDATE app_features SET path = '/sales/customers',           permission = 'perm:sales.customers.view'              WHERE permission = 'perm:ventas.customers.view';
UPDATE app_features SET path = '/sales/withholding-received',permission = 'perm:sales.withholding-received.view'   WHERE permission = 'perm:ventas.retenciones-recibidas.list';
UPDATE app_features SET path = '/purchases/invoices',        permission = 'perm:purchases.invoices.view'           WHERE permission = 'perm:compras.facturas.view';
UPDATE app_features SET path = '/purchases/suppliers',       permission = 'perm:purchases.suppliers.view'          WHERE permission = 'perm:compras.proveedores.view';
UPDATE app_features SET path = '/purchases/orders',          permission = 'perm:purchases.orders.view'             WHERE permission = 'perm:compras.ordenes.view';
UPDATE app_features SET path = '/purchases/credit-notes',    permission = 'perm:purchases.credit-notes.view'       WHERE permission = 'perm:compras.notas-proveedor.view';
UPDATE app_features SET path = '/purchases/withholding-issued',permission = 'perm:purchases.withholding-issued.view' WHERE permission = 'perm:compras.retenciones.list';
UPDATE app_features SET path = '/expenses',                  permission = 'perm:expenses.invoices.view'            WHERE permission = 'perm:gastos.facturas.view';
UPDATE app_features SET path = '/inventory/products',        permission = 'perm:inventory.products.view'           WHERE permission = 'perm:inventario.products.view';
UPDATE app_features SET path = '/inventory/stock',           permission = 'perm:inventory.stock.view'              WHERE permission = 'session:inventario.stock';
UPDATE app_features SET path = '/inventory/warehouses',      permission = 'perm:inventory.warehouses.view'         WHERE permission = 'perm:inventario.bodegas.view';
UPDATE app_features SET path = '/inventory/kardex',          permission = 'perm:inventory.kardex.view'             WHERE permission = 'perm:inventario.kardex.view';
UPDATE app_features SET path = '/inventory/adjustments',     permission = 'perm:inventory.adjustments.view'        WHERE permission = 'perm:inventario.ajustes.view';
UPDATE app_features SET path = '/inventory/transfers',       permission = 'perm:inventory.transfers.view'          WHERE permission = 'perm:inventario.transferencias.view';
UPDATE app_features SET path = '/inventory/brands',          permission = 'perm:inventory.brands.view'             WHERE permission = 'perm:inventario.brands.view';
UPDATE app_features SET path = '/inventory/tariffs',         permission = 'perm:inventory.tariffs.view'            WHERE permission = 'perm:inventario.tariffs.view';
UPDATE app_features SET path = '/inventory/units',           permission = 'perm:inventory.units.view'              WHERE permission = 'perm:inventario.units.view';
-- /inventario/structure tiene 3 entradas — permisos distintos para cada una
UPDATE app_features SET path = '/inventory/catalog-structure', permission = 'perm:inventory.product-lines.view'   WHERE permission = 'perm:inventario.productLines.view';
UPDATE app_features SET path = '/inventory/catalog-structure', permission = 'perm:inventory.categories.view'      WHERE permission = 'perm:inventario.categories.view';
UPDATE app_features SET path = '/inventory/catalog-structure', permission = 'perm:inventory.subcategories.view'   WHERE permission = 'perm:inventario.subcategories.view';
UPDATE app_features SET path = '/inventory/product-types',   permission = 'perm:inventory.product-types.view'     WHERE permission = 'perm:inventario.productTypes.view';
UPDATE app_features SET path = '/logistics/carriers',        permission = 'perm:logistics.carriers.view'          WHERE permission = 'perm:logistica.carriers.view';
UPDATE app_features SET path = '/cash/bank',                 permission = 'perm:cash.bank.view'                    WHERE permission = 'perm:caja.extractos.view';
UPDATE app_features SET path = '/settings/company',          permission = 'perm:settings.company.view'            WHERE permission = 'perm:ventas.configuracion.view' AND path = '/configuracion/empresa';
UPDATE app_features SET path = '/settings/sri',              permission = 'perm:settings.sri.view'                WHERE permission = 'perm:ventas.configuracion.view' AND path = '/configuracion/sri';
UPDATE app_features SET path = '/settings/ride',             permission = 'perm:settings.ride.view'               WHERE permission = 'perm:ventas.configuracion.view' AND path = '/configuracion/facturacion';
UPDATE app_features SET path = '/settings/branches',         permission = 'perm:settings.branches.view'           WHERE permission = 'perm:saas.branches.view';
UPDATE app_features SET path = '/settings/geography',        permission = 'perm:settings.geography.view'          WHERE permission = 'session:geography';
UPDATE app_features SET path = '/admin/roles',               permission = 'perm:admin.roles.view'                  WHERE permission = 'perm:access.profiles.view';
UPDATE app_features SET path = '/admin/users',               permission = 'perm:admin.users.view'                  WHERE permission = 'perm:access.memberships.view';
UPDATE app_features SET path = '/admin/activity',            permission = 'perm:admin.activity.view'              WHERE permission = 'session:activity';
-- Grupo configuracion → settings
UPDATE app_features SET permission = 'perm:settings.group'   WHERE permission = 'perm:configuracion.group';

-- ═══════════════════════════════════════════════════════════════
-- 4. access_profile_permissions (vacía ahora; preparada)
-- ═══════════════════════════════════════════════════════════════
UPDATE access_profile_permissions SET permission_key = 'finance.accounts.view'          WHERE permission_key = 'accounting.accounts.view';
UPDATE access_profile_permissions SET permission_key = 'finance.accounts.create'        WHERE permission_key = 'accounting.accounts.create';
UPDATE access_profile_permissions SET permission_key = 'finance.accounts.edit'          WHERE permission_key = 'accounting.accounts.edit';
UPDATE access_profile_permissions SET permission_key = 'finance.config.view'            WHERE permission_key = 'accounting.config.view';
UPDATE access_profile_permissions SET permission_key = 'finance.config.edit'            WHERE permission_key = 'accounting.config.edit';
UPDATE access_profile_permissions SET permission_key = 'finance.journal.view'           WHERE permission_key = 'accounting.journal.view';
UPDATE access_profile_permissions SET permission_key = 'sales.invoices.view'            WHERE permission_key = 'ventas.invoice.view';
UPDATE access_profile_permissions SET permission_key = 'sales.invoices.create'          WHERE permission_key = 'ventas.invoice.create';
UPDATE access_profile_permissions SET permission_key = 'sales.invoices.update'          WHERE permission_key = 'ventas.invoice.update';
UPDATE access_profile_permissions SET permission_key = 'sales.invoices.void'            WHERE permission_key = 'ventas.invoice.void';
UPDATE access_profile_permissions SET permission_key = 'sales.credit-notes.view'        WHERE permission_key = 'ventas.credit-note.view';
UPDATE access_profile_permissions SET permission_key = 'sales.credit-notes.create'      WHERE permission_key = 'ventas.credit-note.create';
UPDATE access_profile_permissions SET permission_key = 'sales.credit-notes.void'        WHERE permission_key = 'ventas.credit-note.void';
UPDATE access_profile_permissions SET permission_key = 'sales.debit-notes.view'         WHERE permission_key = 'ventas.debit-note.view';
UPDATE access_profile_permissions SET permission_key = 'sales.debit-notes.create'       WHERE permission_key = 'ventas.debit-note.create';
UPDATE access_profile_permissions SET permission_key = 'sales.customers.view'           WHERE permission_key = 'ventas.customers.view';
UPDATE access_profile_permissions SET permission_key = 'sales.customers.create'         WHERE permission_key = 'ventas.customers.create';
UPDATE access_profile_permissions SET permission_key = 'sales.customers.update'         WHERE permission_key = 'ventas.customers.update';
UPDATE access_profile_permissions SET permission_key = 'sales.customers.delete'         WHERE permission_key = 'ventas.customers.delete';
UPDATE access_profile_permissions SET permission_key = 'purchases.orders.view'          WHERE permission_key = 'compras.orders.view';
UPDATE access_profile_permissions SET permission_key = 'purchases.orders.create'        WHERE permission_key = 'compras.orders.create';
UPDATE access_profile_permissions SET permission_key = 'purchases.orders.approve'       WHERE permission_key = 'compras.orders.approve';
UPDATE access_profile_permissions SET permission_key = 'purchases.orders.cancel'        WHERE permission_key = 'compras.orders.cancel';
UPDATE access_profile_permissions SET permission_key = 'purchases.suppliers.view'       WHERE permission_key = 'compras.proveedores.view';
UPDATE access_profile_permissions SET permission_key = 'purchases.suppliers.create'     WHERE permission_key = 'compras.proveedores.create';
UPDATE access_profile_permissions SET permission_key = 'purchases.suppliers.update'     WHERE permission_key = 'compras.proveedores.update';
UPDATE access_profile_permissions SET permission_key = 'purchases.suppliers.delete'     WHERE permission_key = 'compras.proveedores.delete';
UPDATE access_profile_permissions SET permission_key = 'inventory.products.view'        WHERE permission_key = 'inventario.products.view';
UPDATE access_profile_permissions SET permission_key = 'inventory.products.create'      WHERE permission_key = 'inventario.products.create';
UPDATE access_profile_permissions SET permission_key = 'inventory.products.update'      WHERE permission_key = 'inventario.products.update';
UPDATE access_profile_permissions SET permission_key = 'inventory.products.delete'      WHERE permission_key = 'inventario.products.delete';
UPDATE access_profile_permissions SET permission_key = 'inventory.brands.view'          WHERE permission_key = 'inventario.brands.view';
UPDATE access_profile_permissions SET permission_key = 'inventory.brands.create'        WHERE permission_key = 'inventario.brands.create';
UPDATE access_profile_permissions SET permission_key = 'inventory.brands.update'        WHERE permission_key = 'inventario.brands.update';
UPDATE access_profile_permissions SET permission_key = 'inventory.brands.delete'        WHERE permission_key = 'inventario.brands.delete';
UPDATE access_profile_permissions SET permission_key = 'inventory.warehouses.view'      WHERE permission_key = 'inventario.warehouses.view';
UPDATE access_profile_permissions SET permission_key = 'inventory.warehouses.create'    WHERE permission_key = 'inventario.warehouses.create';
UPDATE access_profile_permissions SET permission_key = 'inventory.warehouses.update'    WHERE permission_key = 'inventario.warehouses.update';
UPDATE access_profile_permissions SET permission_key = 'inventory.transfers.view'       WHERE permission_key = 'inventario.transfers.view';
UPDATE access_profile_permissions SET permission_key = 'inventory.transfers.create'     WHERE permission_key = 'inventario.transfers.create';
UPDATE access_profile_permissions SET permission_key = 'inventory.transfers.approve'    WHERE permission_key = 'inventario.transfers.approve';
UPDATE access_profile_permissions SET permission_key = 'inventory.adjustments.view'     WHERE permission_key = 'inventario.adjustments.view';
UPDATE access_profile_permissions SET permission_key = 'inventory.adjustments.create'   WHERE permission_key = 'inventario.adjustments.create';
UPDATE access_profile_permissions SET permission_key = 'inventory.adjustments.approve'  WHERE permission_key = 'inventario.adjustments.approve';
UPDATE access_profile_permissions SET permission_key = 'logistics.carriers.view'        WHERE permission_key = 'logistica.carriers.view';
UPDATE access_profile_permissions SET permission_key = 'logistics.carriers.create'      WHERE permission_key = 'logistica.carriers.create';
UPDATE access_profile_permissions SET permission_key = 'logistics.carriers.update'      WHERE permission_key = 'logistica.carriers.update';
UPDATE access_profile_permissions SET permission_key = 'logistics.carriers.delete'      WHERE permission_key = 'logistica.carriers.delete';
UPDATE access_profile_permissions SET permission_key = 'admin.roles.view'               WHERE permission_key = 'access.profiles.view';
UPDATE access_profile_permissions SET permission_key = 'admin.users.view'               WHERE permission_key = 'access.memberships.view';

-- ═══════════════════════════════════════════════════════════════
-- 5. saas_plans.menu_config (jsonb) — reemplazar rutas
-- ═══════════════════════════════════════════════════════════════
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
    '"/ventas/facturas"',              '"/sales/invoices"',            'g'),
    '"/ventas/customers"',             '"/sales/customers"',           'g'),
    '"/ventas/notas"',                 '"/sales/credit-notes"',        'g'),
    '"/ventas/retenciones-recibidas"', '"/sales/withholding-received"','g'),
    '"/compras/facturas"',             '"/purchases/invoices"',        'g'),
    '"/compras/proveedores"',          '"/purchases/suppliers"',       'g'),
    '"/compras/ordenes"',              '"/purchases/orders"',          'g'),
    '"/compras/notas-proveedor"',      '"/purchases/credit-notes"',    'g'),
    '"/compras/retenciones"',          '"/purchases/withholding-issued"','g'),
    '"/gastos"',                       '"/expenses"',                  'g'),
    '"/inventario/ajustes"',           '"/inventory/adjustments"',     'g'),
    '"/inventario/transferencias"',    '"/inventory/transfers"',       'g'),
    '"/inventario/brands"',            '"/inventory/brands"',          'g'),
    '"/inventario/tariffs"',           '"/inventory/tariffs"',         'g'),
    '"/inventario/units"',             '"/inventory/units"',           'g'),
    '"/inventario/structure"',         '"/inventory/catalog-structure"','g'),
    '"/inventario/product-types"',     '"/inventory/product-types"',   'g'),
    '"/inventario/products"',          '"/inventory/products"',        'g'),
    '"/inventario/bodegas"',           '"/inventory/warehouses"',      'g'),
    '"/logistica/transportistas"',     '"/logistics/carriers"',        'g'),
    '"/configuracion/empresa"',        '"/settings/company"',          'g'),
    '"/configuracion/sri"',            '"/settings/sri"',              'g'),
    '"/configuracion/facturacion"',    '"/settings/ride"',             'g'),
    '"/saas/branches"',                '"/settings/branches"',         'g'),
    '"/profiles"',                     '"/admin/roles"',               'g'),
    '"/access"',                       '"/admin/users"',               'g'),
    '"/actividad"',                    '"/admin/activity"',            'g'),
    '"/accounting"',                   '"/finance/accounts"',          'g')
  )::jsonb
WHERE menu_config IS NOT NULL;
