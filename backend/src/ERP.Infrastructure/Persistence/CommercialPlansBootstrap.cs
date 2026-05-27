using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// Inserta y actualiza el catálogo de planes comerciales en el arranque.
/// Idempotente: no duplica ni sobreescribe JSON personalizado por el operador platform.
/// Planes: Starter · Business · Professional · Enterprise.
/// </summary>
public static class CommercialPlansBootstrap
{
    private sealed record DefaultPlanSeed(
        string Code,
        string Name,
        string ShortLabel,
        decimal PriceAmount,
        bool IsPubliclyVisible,
        bool IsRecommended,
        int SortOrder,
        string? MenuConfigJson = null);

    private static readonly DefaultPlanSeed[] DefaultPlans =
    [
        new("starter",      "Starter",      "STARTER",      49m,  true, false, 0, StarterMenuConfigJson),
        new("business",     "Business",     "BUSINESS",     99m,  true, true,  1, StarterMenuConfigJson),
        new("professional", "Professional", "PROFESSIONAL", 179m, true, false, 2, StarterMenuConfigJson),
        new("enterprise",   "Enterprise",   "ENTERPRISE",   299m, true, false, 3, StarterMenuConfigJson),
    ];

    public static async Task EnsureDefaultsAsync(ErpDbContext db, CancellationToken ct = default)
    {
        var existingPlans = await db.CommercialPlans
            .Where(p => p.Code != null)
            .ToListAsync(ct);

        var existingByCode = existingPlans
            .Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .ToDictionary(p => p.Code!.Trim().ToLowerInvariant());

        var toInsert = new List<CommercialPlan>();
        var changed = false;

        foreach (var seed in DefaultPlans)
        {
            if (!existingByCode.TryGetValue(seed.Code, out var existing))
            {
                var plan = CommercialPlan.Create(
                    code: seed.Code,
                    name: seed.Name,
                    shortLabel: seed.ShortLabel,
                    isActive: true,
                    priceAmount: seed.PriceAmount,
                    currency: "USD",
                    billingCycle: CommercialBillingCycle.Monthly,
                    isPubliclyVisible: seed.IsPubliclyVisible,
                    isRecommended: seed.IsRecommended,
                    sortOrder: seed.SortOrder,
                    externalBillingRef: null);

                if (seed.MenuConfigJson is not null)
                    plan.SetMenuConfigJson(seed.MenuConfigJson);

                toInsert.Add(plan);
                continue;
            }

            // Plan ya existe: inyectar MenuConfigJson si:
            //   a) está vacío/null, o
            //   b) tiene estructura "plan-custom" plana (JSON de migración incorrecto), o
            //   c) contiene rutas legacy de BP (/sales/customers, /purchases/suppliers)
            //      que apuntan a páginas eliminadas tras la unificación BusinessPartner, o
            //   d) contiene rutas de configuración consolidadas en el hub /settings/company.
            var hasPlanCustom      = existing.MenuConfigJson?.Contains("plan-custom") == true;
            var hasLegacyBpRoutes  = existing.MenuConfigJson?.Contains("/sales/customers") == true
                                   || existing.MenuConfigJson?.Contains("/purchases/suppliers") == true;
            var hasFinanceConfigNav = existing.MenuConfigJson?.Contains("\"/finance/config\"") == true;
            var hasDeprecatedSettingsRoutes =
                   existing.MenuConfigJson?.Contains("\"/settings/sri\"")      == true
                || existing.MenuConfigJson?.Contains("\"/settings/branches\"") == true;
            if (seed.MenuConfigJson is not null &&
                (string.IsNullOrWhiteSpace(existing.MenuConfigJson) || hasPlanCustom || hasLegacyBpRoutes || hasFinanceConfigNav || hasDeprecatedSettingsRoutes))
            {
                existing.SetMenuConfigJson(seed.MenuConfigJson);
                changed = true;
            }
        }

        if (toInsert.Count > 0)
        {
            db.CommercialPlans.AddRange(toInsert);
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }

    private const string StarterMenuConfigJson = """
        [
          {
            "code": "sales", "icon": "🧾", "labelKey": "app.nav.group.sales",
            "sortOrder": 10, "moduleKey": "sales", "roles": null,
            "requirePlatformPanel": false, "menuBarLayout": null,
            "items": [
              {"routePath": "/sales/invoices",             "labelKey": "app.nav.sales.invoices",                  "displayLabel": "Facturas",              "sortOrder": 10, "moduleKey": "sales", "permissionKey": "perm:sales.invoices.view",                    "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "🧾"},
              {"routePath": "/sales/quotes",               "labelKey": "app.nav.sales.quotes",                    "displayLabel": "Cotizaciones",          "sortOrder": 15, "moduleKey": "sales", "permissionKey": "perm:sales.quotes.view",                      "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "📝"},
              {"routePath": "/sales/orders",               "labelKey": "app.nav.sales.orders",                    "displayLabel": "Pedidos",               "sortOrder": 17, "moduleKey": "sales", "permissionKey": "perm:sales.orders.view",                      "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "📋"},
              {"routePath": "/masterdata/customers",       "labelKey": "app.nav.catalog.customers",               "displayLabel": "Clientes",              "sortOrder": 20, "moduleKey": "sales", "permissionKey": "perm:masterdata.businesspartners.view",       "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "👤"},
              {"routePath": "/sales/credit-notes",         "labelKey": "app.nav.item.sales.credit-notes",         "displayLabel": "SalesNotes de crédito",      "sortOrder": 30, "moduleKey": "sales", "permissionKey": "perm:sales.credit-notes.view",               "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "📄"},
              {"routePath": "/sales/withholding-received", "labelKey": "app.nav.item.sales.withholding-received", "displayLabel": "Retentions recibidas", "sortOrder": 40, "moduleKey": "sales", "permissionKey": "perm:sales.withholding-received.view",        "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "📋"},
              {"routePath": "", "labelKey": "app.nav.item.sales.reports", "displayLabel": "Reportes", "sortOrder": 50, "moduleKey": "sales", "permissionKey": null, "permissionKeysAny": null, "itemRoles": null, "icon": "bar_chart",
                "children": [
                  {"routePath": "/reportes/ventas", "labelKey": "app.nav.item.sales.report-ventas", "displayLabel": "Reporte de ventas", "sortOrder": 10, "moduleKey": "sales", "permissionKey": "perm:sales.invoices.view", "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "receipt_long"}
                ]}
            ]
          },
          {
            "code": "purchases", "icon": "🛒", "labelKey": "app.nav.group.purchases",
            "sortOrder": 20, "moduleKey": "purchases", "roles": null,
            "requirePlatformPanel": false, "menuBarLayout": null,
            "items": [
              {"routePath": "/purchases/invoices",           "labelKey": "app.nav.item.purchases.invoices",           "displayLabel": "Facturas de compra",      "sortOrder": 10, "moduleKey": "purchases", "permissionKey": "perm:purchases.invoices.view",                  "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "🧾"},
              {"routePath": "/masterdata/suppliers",         "labelKey": "app.nav.item.purchases.suppliers",          "displayLabel": "Proveedores",             "sortOrder": 20, "moduleKey": "purchases", "permissionKey": "perm:masterdata.businesspartners.view",         "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "🏭"},
              {"routePath": "/purchases/orders",             "labelKey": "app.nav.item.purchases.orders",             "displayLabel": "Órdenes de compra",       "sortOrder": 30, "moduleKey": "purchases", "permissionKey": "perm:purchases.orders.view",                    "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "📋"},
              {"routePath": "/purchases/credit-notes",       "labelKey": "app.nav.item.purchases.credit-notes",       "displayLabel": "SalesNotes crédito proveedor", "sortOrder": 40, "moduleKey": "purchases", "permissionKey": "perm:purchases.credit-notes.view",              "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "📄"},
              {"routePath": "/purchases/withholding-issued", "labelKey": "app.nav.item.purchases.withholding-issued", "displayLabel": "Retentions emitidas",    "sortOrder": 50, "moduleKey": "purchases", "permissionKey": "perm:purchases.withholding-issued.view",        "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "📋"}
            ]
          },
          {
            "code": "inventory", "icon": "📦", "labelKey": "app.nav.group.inventory",
            "sortOrder": 30, "moduleKey": "inventory", "roles": null,
            "requirePlatformPanel": false, "menuBarLayout": null,
            "items": [
              {"routePath": "/inventory/products",          "labelKey": "app.nav.item.inventory.products",   "displayLabel": "Productos",           "sortOrder": 10,  "moduleKey": "inventory", "permissionKey": "perm:inventory.products.view",      "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "📦"},
              {"routePath": "/inventory/stock",             "labelKey": "app.nav.item.inventory.stock",      "displayLabel": "Stock por bodega",    "sortOrder": 20,  "moduleKey": "inventory", "permissionKey": "perm:inventory.stock.view",         "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "🏪"},
              {"routePath": "/inventory/warehouses",        "labelKey": "app.nav.item.inventory.warehouses", "displayLabel": "Bodegas",             "sortOrder": 30,  "moduleKey": "inventory", "permissionKey": "perm:inventory.warehouses.view",    "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "🏬"},
              {"routePath": "/inventory/kardex",            "labelKey": "app.nav.item.inventory.kardex",     "displayLabel": "Kardex",              "sortOrder": 40,  "moduleKey": "inventory", "permissionKey": "perm:inventory.kardex.view",        "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "📊"},
              {"routePath": "/inventory/adjustments",       "labelKey": "app.nav.catalog.ajustes",           "displayLabel": "Ajustes inventario",  "sortOrder": 50,  "moduleKey": "inventory", "permissionKey": "perm:inventory.adjustments.view",   "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "⚖️"},
              {"routePath": "/inventory/transfers",         "labelKey": "app.nav.catalog.transferencias",    "displayLabel": "Transferencias",      "sortOrder": 60,  "moduleKey": "inventory", "permissionKey": "perm:inventory.transfers.view",     "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "↔️"},
              {"routePath": "/inventory/brands",            "labelKey": "app.nav.catalog.brands",            "displayLabel": "Marcas",              "sortOrder": 70,  "moduleKey": "inventory", "permissionKey": "perm:inventory.brands.view",        "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "🏷️"},
              {"routePath": "/inventory/tariffs",           "labelKey": "app.nav.catalog.tariffs",           "displayLabel": "Tarifas",             "sortOrder": 80,  "moduleKey": "inventory", "permissionKey": "perm:inventory.tariffs.view",       "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "💱"},
              {"routePath": "/inventory/units",             "labelKey": "app.nav.catalog.units",             "displayLabel": "Unidades de medida",  "sortOrder": 90,  "moduleKey": "inventory", "permissionKey": "perm:inventory.units.view",         "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "📏"},
              {"routePath": "/inventory/catalog-structure", "labelKey": "app.nav.catalog.structure",         "displayLabel": "Estructura catálogo", "sortOrder": 100, "moduleKey": "inventory", "permissionKey": "perm:inventory.catalog.view",       "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "🗂️"},
              {"routePath": "/inventory/product-types",     "labelKey": "app.nav.catalog.productTypes",      "displayLabel": "Tipos de producto",   "sortOrder": 110, "moduleKey": "inventory", "permissionKey": "perm:inventory.product-types.view", "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "🔖"}
            ]
          },
          {
            "code": "logistics", "icon": "🚚", "labelKey": "app.nav.group.logistics",
            "sortOrder": 40, "moduleKey": "logistics", "roles": null,
            "requirePlatformPanel": false, "menuBarLayout": null,
            "items": [
              {"routePath": "/logistics/carriers", "labelKey": "app.nav.item.logistics.carriers", "displayLabel": "Transportistas", "sortOrder": 10, "moduleKey": "logistics", "permissionKey": "perm:logistics.carriers.view", "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "🚛"}
            ]
          },
          {
            "code": "cash", "icon": "💳", "labelKey": "app.nav.group.cash",
            "sortOrder": 50, "moduleKey": "cash", "roles": null,
            "requirePlatformPanel": false, "menuBarLayout": null,
            "items": [
              {"routePath": "/cash/bank", "labelKey": "app.nav.item.cash.bank", "displayLabel": "Caja y bancos", "sortOrder": 10, "moduleKey": "cash", "permissionKey": "perm:cash.bank.view", "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "🏦"}
            ]
          },
          {
            "code": "finance", "icon": "📒", "labelKey": "app.nav.group.finance",
            "sortOrder": 60, "moduleKey": "finance", "roles": null,
            "requirePlatformPanel": false, "menuBarLayout": null,
            "items": [
              {"routePath": "/finance/accounts", "labelKey": "app.nav.item.finance.accounts", "displayLabel": "Contabilidad", "sortOrder": 10, "moduleKey": "finance", "permissionKey": "perm:finance.accounts.view", "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "📒"}
            ]
          },
          {
            "code": "expenses", "icon": "💸", "labelKey": "app.nav.group.expenses",
            "sortOrder": 70, "moduleKey": "expenses", "roles": null,
            "requirePlatformPanel": false, "menuBarLayout": null,
            "items": [
              {"routePath": "/expenses", "labelKey": "app.nav.item.expenses.invoices", "displayLabel": "Gastos", "sortOrder": 10, "moduleKey": "expenses", "permissionKey": "perm:expenses.invoices.view", "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "💸"}
            ]
          },
          {
            "code": "settings", "icon": "⚙", "labelKey": "app.nav.group.settings",
            "sortOrder": 80, "moduleKey": "settings", "roles": null,
            "requirePlatformPanel": false, "menuBarLayout": null,
            "items": [
              {"routePath": "/settings/company",   "labelKey": "app.nav.item.settings.company",   "displayLabel": "Configuración Empresa", "sortOrder": 10, "moduleKey": "settings", "permissionKey": "perm:settings.company.view",   "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "tune"},
              {"routePath": "/settings/ride",      "labelKey": "app.nav.item.settings.ride",      "displayLabel": "Configuración RIDE",    "sortOrder": 20, "moduleKey": "settings", "permissionKey": "perm:settings.ride.view",      "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "🖨️"},
              {"routePath": "/settings/geography", "labelKey": "app.nav.item.settings.geography", "displayLabel": "Geografía",             "sortOrder": 30, "moduleKey": "settings", "permissionKey": "perm:settings.geography.view", "permissionKeysAny": null, "itemRoles": null, "children": null, "icon": "🌎"}
            ]
          },
          {
            "code": "admin", "icon": "🛡️", "labelKey": "app.nav.group.admin",
            "sortOrder": 90, "moduleKey": "admin", "roles": null,
            "requirePlatformPanel": false, "menuBarLayout": null,
            "items": [
              {"routePath": "/admin/roles",    "labelKey": "app.nav.item.admin.roles",    "displayLabel": "Perfiles (Roles)", "sortOrder": 10, "moduleKey": "admin", "permissionKey": "perm:admin.roles.view",    "permissionKeysAny": null, "itemRoles": null,               "children": null, "icon": "👥"},
              {"routePath": "/admin/users",    "labelKey": "app.nav.item.admin.users",    "displayLabel": "Acceso usuarios",  "sortOrder": 20, "moduleKey": "admin", "permissionKey": "perm:admin.users.view",    "permissionKeysAny": null, "itemRoles": null,               "children": null, "icon": "👤"},
              {"routePath": "/admin/activity", "labelKey": "app.nav.item.admin.activity", "displayLabel": "Actividad",        "sortOrder": 30, "moduleKey": "admin", "permissionKey": "perm:admin.activity.view", "permissionKeysAny": null, "itemRoles": null,               "children": null, "icon": "📜"},
              {"routePath": "/admin/security", "labelKey": "app.nav.item.admin.security", "displayLabel": "Seguridad",        "sortOrder": 40, "moduleKey": "admin", "permissionKey": null,                        "permissionKeysAny": null, "itemRoles": ["PlatformOperator", "Admin"], "children": null, "icon": "🔒"}
            ]
          }
        ]
        """;
}
