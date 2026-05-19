-- ============================================================================
-- 004_starter_menu_fix.sql
-- Reemplaza menu_config del plan "starter" con la estructura correcta de grupos.
-- Idempotente: solo actúa si menu_config es NULL, vacío, o tiene "plan-custom".
-- No usar BEGIN/COMMIT: InstallDataBootstrapService ejecuta en transacción propia.
-- ============================================================================

UPDATE saas_plans
SET menu_config = $starter_menu$
[
  {
    "code": "sales",
    "icon": "🧾",
    "labelKey": "app.nav.group.sales",
    "sortOrder": 10,
    "moduleKey": "sales",
    "roles": null,
    "requireSuperAdminPanel": false,
    "menuBarLayout": null,
    "items": [
      {
        "routePath": "/sales/invoices",
        "labelKey": "app.nav.sales.invoices",
        "displayLabel": "Facturas",
        "sortOrder": 10,
        "moduleKey": "sales",
        "permissionKey": "perm:sales.invoices.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🧾"
      },
      {
        "routePath": "/sales/customers",
        "labelKey": "app.nav.catalog.customers",
        "displayLabel": "Clientes",
        "sortOrder": 20,
        "moduleKey": "sales",
        "permissionKey": "perm:sales.customers.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "👤"
      },
      {
        "routePath": "/sales/credit-notes",
        "labelKey": "app.nav.item.sales.credit-notes",
        "displayLabel": "Notas de crédito",
        "sortOrder": 30,
        "moduleKey": "sales",
        "permissionKey": "perm:sales.credit-notes.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "📄"
      },
      {
        "routePath": "/sales/withholding-received",
        "labelKey": "app.nav.item.sales.withholding-received",
        "displayLabel": "Retenciones recibidas",
        "sortOrder": 40,
        "moduleKey": "sales",
        "permissionKey": "perm:sales.withholding-received.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "📋"
      }
    ]
  },
  {
    "code": "purchases",
    "icon": "🛒",
    "labelKey": "app.nav.group.purchases",
    "sortOrder": 20,
    "moduleKey": "purchases",
    "roles": null,
    "requireSuperAdminPanel": false,
    "menuBarLayout": null,
    "items": [
      {
        "routePath": "/purchases/invoices",
        "labelKey": "app.nav.item.purchases.invoices",
        "displayLabel": "Facturas de compra",
        "sortOrder": 10,
        "moduleKey": "purchases",
        "permissionKey": "perm:purchases.invoices.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🧾"
      },
      {
        "routePath": "/purchases/suppliers",
        "labelKey": "app.nav.item.purchases.suppliers",
        "displayLabel": "Proveedores",
        "sortOrder": 20,
        "moduleKey": "purchases",
        "permissionKey": "perm:purchases.suppliers.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🏭"
      },
      {
        "routePath": "/purchases/orders",
        "labelKey": "app.nav.item.purchases.orders",
        "displayLabel": "Órdenes de compra",
        "sortOrder": 30,
        "moduleKey": "purchases",
        "permissionKey": "perm:purchases.orders.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "📋"
      },
      {
        "routePath": "/purchases/credit-notes",
        "labelKey": "app.nav.item.purchases.credit-notes",
        "displayLabel": "Notas crédito proveedor",
        "sortOrder": 40,
        "moduleKey": "purchases",
        "permissionKey": "perm:purchases.credit-notes.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "📄"
      },
      {
        "routePath": "/purchases/withholding-issued",
        "labelKey": "app.nav.item.purchases.withholding-issued",
        "displayLabel": "Retenciones emitidas",
        "sortOrder": 50,
        "moduleKey": "purchases",
        "permissionKey": "perm:purchases.withholding-issued.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "📋"
      }
    ]
  },
  {
    "code": "inventory",
    "icon": "📦",
    "labelKey": "app.nav.group.inventory",
    "sortOrder": 30,
    "moduleKey": "inventory",
    "roles": null,
    "requireSuperAdminPanel": false,
    "menuBarLayout": null,
    "items": [
      {
        "routePath": "/inventory/products",
        "labelKey": "app.nav.item.inventory.products",
        "displayLabel": "Productos",
        "sortOrder": 10,
        "moduleKey": "inventory",
        "permissionKey": "perm:inventory.products.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "📦"
      },
      {
        "routePath": "/inventory/stock",
        "labelKey": "app.nav.item.inventory.stock",
        "displayLabel": "Stock por bodega",
        "sortOrder": 20,
        "moduleKey": "inventory",
        "permissionKey": "perm:inventory.stock.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🏪"
      },
      {
        "routePath": "/inventory/warehouses",
        "labelKey": "app.nav.item.inventory.warehouses",
        "displayLabel": "Bodegas",
        "sortOrder": 30,
        "moduleKey": "inventory",
        "permissionKey": "perm:inventory.warehouses.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🏬"
      },
      {
        "routePath": "/inventory/kardex",
        "labelKey": "app.nav.item.inventory.kardex",
        "displayLabel": "Kardex",
        "sortOrder": 40,
        "moduleKey": "inventory",
        "permissionKey": "perm:inventory.kardex.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "📊"
      },
      {
        "routePath": "/inventory/adjustments",
        "labelKey": "app.nav.catalog.ajustes",
        "displayLabel": "Ajustes inventario",
        "sortOrder": 50,
        "moduleKey": "inventory",
        "permissionKey": "perm:inventory.adjustments.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "⚖️"
      },
      {
        "routePath": "/inventory/transfers",
        "labelKey": "app.nav.catalog.transferencias",
        "displayLabel": "Transferencias",
        "sortOrder": 60,
        "moduleKey": "inventory",
        "permissionKey": "perm:inventory.transfers.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "↔️"
      },
      {
        "routePath": "/inventory/brands",
        "labelKey": "app.nav.catalog.brands",
        "displayLabel": "Marcas",
        "sortOrder": 70,
        "moduleKey": "inventory",
        "permissionKey": "perm:inventory.brands.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🏷️"
      },
      {
        "routePath": "/inventory/tariffs",
        "labelKey": "app.nav.catalog.tariffs",
        "displayLabel": "Tarifas",
        "sortOrder": 80,
        "moduleKey": "inventory",
        "permissionKey": "perm:inventory.tariffs.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "💱"
      },
      {
        "routePath": "/inventory/units",
        "labelKey": "app.nav.catalog.units",
        "displayLabel": "Unidades de medida",
        "sortOrder": 90,
        "moduleKey": "inventory",
        "permissionKey": "perm:inventory.units.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "📏"
      },
      {
        "routePath": "/inventory/catalog-structure",
        "labelKey": "app.nav.catalog.structure",
        "displayLabel": "Estructura catálogo",
        "sortOrder": 100,
        "moduleKey": "inventory",
        "permissionKey": "perm:inventory.catalog.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🗂️"
      },
      {
        "routePath": "/inventory/product-types",
        "labelKey": "app.nav.catalog.productTypes",
        "displayLabel": "Tipos de producto",
        "sortOrder": 110,
        "moduleKey": "inventory",
        "permissionKey": "perm:inventory.product-types.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🔖"
      }
    ]
  },
  {
    "code": "logistics",
    "icon": "🚚",
    "labelKey": "app.nav.group.logistics",
    "sortOrder": 40,
    "moduleKey": "logistics",
    "roles": null,
    "requireSuperAdminPanel": false,
    "menuBarLayout": null,
    "items": [
      {
        "routePath": "/logistics/carriers",
        "labelKey": "app.nav.item.logistics.carriers",
        "displayLabel": "Transportistas",
        "sortOrder": 10,
        "moduleKey": "logistics",
        "permissionKey": "perm:logistics.carriers.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🚛"
      }
    ]
  },
  {
    "code": "cash",
    "icon": "💳",
    "labelKey": "app.nav.group.cash",
    "sortOrder": 50,
    "moduleKey": "cash",
    "roles": null,
    "requireSuperAdminPanel": false,
    "menuBarLayout": null,
    "items": [
      {
        "routePath": "/cash/bank",
        "labelKey": "app.nav.item.cash.bank",
        "displayLabel": "Caja y bancos",
        "sortOrder": 10,
        "moduleKey": "cash",
        "permissionKey": "perm:cash.bank.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🏦"
      }
    ]
  },
  {
    "code": "finance",
    "icon": "📒",
    "labelKey": "app.nav.group.finance",
    "sortOrder": 60,
    "moduleKey": "finance",
    "roles": null,
    "requireSuperAdminPanel": false,
    "menuBarLayout": null,
    "items": [
      {
        "routePath": "/finance/accounts",
        "labelKey": "app.nav.item.finance.accounts",
        "displayLabel": "Contabilidad",
        "sortOrder": 10,
        "moduleKey": "finance",
        "permissionKey": "perm:finance.accounts.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "📒"
      },
      {
        "routePath": "/finance/config",
        "labelKey": "app.nav.item.finance.config",
        "displayLabel": "Config. contable",
        "sortOrder": 20,
        "moduleKey": "finance",
        "permissionKey": "perm:finance.config.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "⚙️"
      }
    ]
  },
  {
    "code": "expenses",
    "icon": "💸",
    "labelKey": "app.nav.group.expenses",
    "sortOrder": 70,
    "moduleKey": "expenses",
    "roles": null,
    "requireSuperAdminPanel": false,
    "menuBarLayout": null,
    "items": [
      {
        "routePath": "/expenses",
        "labelKey": "app.nav.item.expenses.invoices",
        "displayLabel": "Gastos",
        "sortOrder": 10,
        "moduleKey": "expenses",
        "permissionKey": "perm:expenses.invoices.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "💸"
      }
    ]
  },
  {
    "code": "settings",
    "icon": "⚙",
    "labelKey": "app.nav.group.settings",
    "sortOrder": 80,
    "moduleKey": "settings",
    "roles": null,
    "requireSuperAdminPanel": false,
    "menuBarLayout": null,
    "items": [
      {
        "routePath": "/settings/company",
        "labelKey": "app.nav.item.settings.company",
        "displayLabel": "Datos de Empresa",
        "sortOrder": 10,
        "moduleKey": "settings",
        "permissionKey": "perm:settings.company.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🏢"
      },
      {
        "routePath": "/settings/sri",
        "labelKey": "app.nav.item.settings.sri",
        "displayLabel": "Configuración SRI",
        "sortOrder": 20,
        "moduleKey": "settings",
        "permissionKey": "perm:settings.sri.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🧾"
      },
      {
        "routePath": "/settings/ride",
        "labelKey": "app.nav.item.settings.ride",
        "displayLabel": "Configuración RIDE",
        "sortOrder": 30,
        "moduleKey": "settings",
        "permissionKey": "perm:settings.ride.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🖨️"
      },
      {
        "routePath": "/settings/branches",
        "labelKey": "app.nav.item.settings.branches",
        "displayLabel": "Sucursales",
        "sortOrder": 40,
        "moduleKey": "settings",
        "permissionKey": "perm:settings.branches.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🏪"
      },
      {
        "routePath": "/settings/geography",
        "labelKey": "app.nav.item.settings.geography",
        "displayLabel": "Geografía",
        "sortOrder": 50,
        "moduleKey": "settings",
        "permissionKey": "perm:settings.geography.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "🌎"
      }
    ]
  },
  {
    "code": "admin",
    "icon": "🛡️",
    "labelKey": "app.nav.group.admin",
    "sortOrder": 90,
    "moduleKey": "admin",
    "roles": null,
    "requireSuperAdminPanel": false,
    "menuBarLayout": null,
    "items": [
      {
        "routePath": "/admin/roles",
        "labelKey": "app.nav.item.admin.roles",
        "displayLabel": "Perfiles (Roles)",
        "sortOrder": 10,
        "moduleKey": "admin",
        "permissionKey": "perm:admin.roles.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "👥"
      },
      {
        "routePath": "/admin/users",
        "labelKey": "app.nav.item.admin.users",
        "displayLabel": "Acceso usuarios",
        "sortOrder": 20,
        "moduleKey": "admin",
        "permissionKey": "perm:admin.users.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "👤"
      },
      {
        "routePath": "/admin/activity",
        "labelKey": "app.nav.item.admin.activity",
        "displayLabel": "Actividad",
        "sortOrder": 30,
        "moduleKey": "admin",
        "permissionKey": "perm:admin.activity.view",
        "permissionKeysAny": null,
        "itemRoles": null,
        "children": null,
        "icon": "📜"
      }
    ]
  }
]
$starter_menu$::jsonb
WHERE code = 'starter'
  AND (
    menu_config IS NULL
    OR trim(menu_config::text) = ''
    OR menu_config::text LIKE '%plan-custom%'
  );
