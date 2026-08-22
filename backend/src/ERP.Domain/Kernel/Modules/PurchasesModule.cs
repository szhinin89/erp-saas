using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MENU-MODULE-REORG-01: reorganizado en Operación/Configuración/Reportes. Cuentas por pagar y
// Créditos de proveedor (antes FinanceModule) y Reporte de Compras (antes ReportsModule) se
// movieron aquí — mismos Ids/rutas/permisos.
[Module("purchases", Icon = "🛒", SortOrder = 30)]
public static class PurchasesModule
{
    // ── Operación ────────────────────────────────────────────────────
    [NavItem(
        "Operación",
        LabelKey = "app.nav.item.purchases.operation",
        SortOrder = 10,
        Id = "e3000000-0000-4000-9000-000000000010",
        PermissionsAnyCsv = PurchasePermissions.View + "," + FinancePermissions.View
    )]
    public const string OperationGroup = "/purchases/operation-group";

    [NavItem(
        "Compras",
        Permission = PurchasePermissions.View,
        LabelKey = "app.nav.item.purchases.invoices",
        SortOrder = 10,
        Id = "c1000000-0000-4000-9000-000000000001",
        ParentId = "e3000000-0000-4000-9000-000000000010"
    )]
    public const string Invoices = "/purchases";

    [NavItem(
        "Recepción electrónica (TXT)",
        Permission = PurchasePermissions.View,
        LabelKey = "app.nav.item.purchases.reception",
        SortOrder = 20,
        Id = "c1000000-0000-4000-9000-000000000002",
        ParentId = "e3000000-0000-4000-9000-000000000010"
    )]
    public const string Reception = "/purchases/reception";

    [NavItem(
        "Devoluciones de compra",
        Permission = PurchasePermissions.View,
        LabelKey = "app.nav.item.purchases.returns",
        SortOrder = 30,
        Id = "c1000000-0000-4000-9000-000000000003",
        ParentId = "e3000000-0000-4000-9000-000000000010"
    )]
    public const string Returns = "/purchases/returns";

    // Movido desde FinanceModule (antes /finance/payables en el grupo "finance" separado) —
    // mismo Id/ruta/permiso, ahora dentro de Compras → Operación.
    [NavItem(
        "Cuentas por pagar",
        Permission = PurchasePermissions.View,
        LabelKey = "app.nav.item.finance.payables",
        SortOrder = 40,
        Id = "f6000000-0000-4000-9000-000000000002",
        ParentId = "e3000000-0000-4000-9000-000000000010"
    )]
    public const string Payables = "/finance/payables";

    // Movido desde FinanceModule (antes /finance/supplier-credits en el grupo "finance"
    // separado) — mismo Id/ruta/permiso, ahora dentro de Compras → Operación.
    [NavItem(
        "Créditos de proveedor",
        Permission = FinancePermissions.View,
        LabelKey = "app.nav.item.finance.supplierCredits",
        SortOrder = 50,
        Id = "f6000000-0000-4000-9000-000000000003",
        ParentId = "e3000000-0000-4000-9000-000000000010"
    )]
    public const string SupplierCredits = "/finance/supplier-credits";

    // ── Configuración ────────────────────────────────────────────────
    [NavItem(
        "Configuración",
        LabelKey = "app.nav.item.purchases.configuration",
        SortOrder = 20,
        Id = "e3000000-0000-4000-9000-000000000020",
        PermissionsAnyCsv = OperationalPreferencesPermissions.View
    )]
    public const string ConfigurationGroup = "/purchases/configuration-group";

    // Enlace contextual al tab "purchases" de la pantalla única de Preferencias Operativas
    // (/settings/operations) — no duplica la pantalla, solo la referencia con deep-link.
    [NavItem(
        "Preferencias de Compras",
        Permission = OperationalPreferencesPermissions.View,
        LabelKey = "app.nav.item.purchases.preferences",
        SortOrder = 10,
        Id = "e3000000-0000-4000-9000-000000000021",
        ParentId = "e3000000-0000-4000-9000-000000000020"
    )]
    public const string Preferences = "/settings/operations?tab=purchases";

    // ── Reportes ─────────────────────────────────────────────────────
    [NavItem(
        "Reportes",
        LabelKey = "app.nav.item.purchases.reports",
        SortOrder = 30,
        Id = "e3000000-0000-4000-9000-000000000030",
        PermissionsAnyCsv = PurchasePermissions.View
    )]
    public const string ReportsGroup = "/purchases/reports-group";

    // Movido desde ReportsModule (antes /reportes/compras en el grupo "reports" separado) —
    // mismo Id/ruta/permiso, ahora dentro de Compras → Reportes.
    [NavItem(
        "Reporte de Compras",
        Permission = PurchasePermissions.View,
        LabelKey = "app.nav.item.reportes.compras",
        SortOrder = 10,
        Id = "f7000000-0000-4000-9000-000000000003",
        ParentId = "e3000000-0000-4000-9000-000000000030"
    )]
    public const string PurchasesReport = "/reportes/compras";
}
