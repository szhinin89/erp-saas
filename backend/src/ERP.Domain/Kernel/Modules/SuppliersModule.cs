using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// NAVIGATION-OPERATING-CYCLES-03: nuevo módulo — concentra el ciclo proveedor, fusionando
// MasterDataModule.Suppliers + PurchasesModule + ExpensesModule + PayablesModule (antes 4 grupos
// separados: "Clientes y proveedores"(parcial), Compras, Gastos, Cuentas por Pagar). Mismos
// Ids/rutas/permisos que tenían en sus módulos de origen, salvo Cuentas por pagar / Pagos a
// proveedores: antes derivaban su Id automáticamente de module.Code="payables"; al cambiar de
// módulo se les fija un Id explícito nuevo para no dejar huérfana la fila vieja en ui_nav_items
// (mismo criterio que "Electronic Invoicing" en SettingsModule).
[Module("suppliers", Icon = "🛒", SortOrder = 12)]
public static class SuppliersModule
{
    // Movido desde MasterDataModule — mismo Id/ruta/permiso.
    [NavItem(
        "Proveedores",
        Permission = MasterDataPermissions.BusinessPartnersView,
        LabelKey = "app.nav.item.masterdata.suppliers",
        SortOrder = 5,
        Id = "a1000000-0000-4000-9000-000000000102"
    )]
    public const string Suppliers = "/masterdata/suppliers";

    // ── Compras (movido desde PurchasesModule, sin cambios internos) ──────────────────
    [NavItem(
        "Compras",
        LabelKey = "app.nav.item.purchases.operation",
        SortOrder = 10,
        Id = "e3000000-0000-4000-9000-000000000010",
        PermissionsAnyCsv = PurchasePermissions.View + "," + FinancePermissions.View
    )]
    public const string PurchasesGroup = "/purchases/operation-group";

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

    [NavItem(
        "Créditos de proveedor",
        Permission = FinancePermissions.View,
        LabelKey = "app.nav.item.finance.supplierCredits",
        SortOrder = 50,
        Id = "f6000000-0000-4000-9000-000000000003",
        ParentId = "e3000000-0000-4000-9000-000000000010"
    )]
    public const string SupplierCredits = "/finance/supplier-credits";

    // ── Gastos (movido desde ExpensesModule, ítems planos sin cambios) ─────────────────
    [NavItem(
        "Documentos de Gastos",
        Permission = ExpensePermissions.DocumentsView,
        LabelKey = "app.nav.item.expenses.documents",
        SortOrder = 20,
        Id = "e5000000-0000-4000-9000-000000000002"
    )]
    public const string ExpenseDocuments = "/expenses/documents";

    [NavItem(
        "Catalogo de Gastos",
        Permission = ExpensePermissions.CatalogView,
        LabelKey = "app.nav.item.expenses.catalog",
        SortOrder = 21,
        Id = "e5000000-0000-4000-9000-000000000001"
    )]
    public const string ExpenseCatalog = "/expenses/categories";

    // ── Cuentas por pagar (movido desde PayablesModule) ────────────────────────────────
    [NavItem(
        "Cuentas por pagar",
        Permission = PayablesPermissions.View,
        LabelKey = "app.nav.item.payables.list",
        SortOrder = 30,
        Id = "c9000000-0000-4000-9000-000000000001"
    )]
    public const string Payables = "/payables";

    [NavItem(
        "Pagos a proveedores",
        Permission = SupplierPaymentsPermissions.View,
        LabelKey = "app.nav.item.payables.supplierPayments",
        SortOrder = 40,
        Id = "c9000000-0000-4000-9000-000000000002"
    )]
    public const string SupplierPayments = "/supplier-payments";

    // ── Configuración (movido desde PurchasesModule) ───────────────────────────────────
    [NavItem(
        "Configuración",
        LabelKey = "app.nav.item.purchases.configuration",
        SortOrder = 50,
        Id = "e3000000-0000-4000-9000-000000000020",
        PermissionsAnyCsv = OperationalPreferencesPermissions.View
    )]
    public const string ConfigurationGroup = "/purchases/configuration-group";

    [NavItem(
        "Preferencias de Compras",
        Permission = OperationalPreferencesPermissions.View,
        LabelKey = "app.nav.item.purchases.preferences",
        SortOrder = 10,
        Id = "e3000000-0000-4000-9000-000000000021",
        ParentId = "e3000000-0000-4000-9000-000000000020"
    )]
    public const string Preferences = "/settings/operations?tab=purchases";

    // ── Reportes (movido desde PurchasesModule) ────────────────────────────────────────
    [NavItem(
        "Reportes",
        LabelKey = "app.nav.item.purchases.reports",
        SortOrder = 60,
        Id = "e3000000-0000-4000-9000-000000000030",
        PermissionsAnyCsv = PurchasePermissions.View
    )]
    public const string ReportsGroup = "/purchases/reports-group";

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
