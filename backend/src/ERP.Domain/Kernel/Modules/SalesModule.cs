using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MENU-MODULE-REORG-01: reorganizado en Operación/Configuración/Reportes. Cuentas por cobrar
// (antes FinanceModule) y Reporte de Ventas (antes ReportsModule) se movieron aquí — mismos
// Ids/rutas/permisos. Facturación Electrónica (config) se movió a Configuración general
// (SettingsModule) por ser transversal; el Monitor de Documentos Electrónicos se mantiene
// aquí como operación de Ventas (revisión diaria de comprobantes emitidos).
[Module("sales", Icon = "💰", SortOrder = 40)]
public static class SalesModule
{
    // ── Operación ────────────────────────────────────────────────────
    [NavItem(
        "Operación",
        LabelKey = "app.nav.item.sales.operation",
        SortOrder = 10,
        Id = "e4000000-0000-4000-9000-000000000010",
        PermissionsAnyCsv = SalesPermissions.View + "," + ElectronicDocumentsPermissions.View
    )]
    public const string OperationGroup = "/sales/operation-group";

    [NavItem(
        "Facturas de venta / Punto de venta",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.sales.invoices",
        SortOrder = 10,
        Id = "d1000000-0000-4000-9000-000000000001",
        ParentId = "e4000000-0000-4000-9000-000000000010"
    )]
    public const string Invoices = "/sales";

    [NavItem(
        "Devoluciones de venta",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.sales.returns",
        SortOrder = 20,
        Id = "d1000000-0000-4000-9000-000000000003",
        ParentId = "e4000000-0000-4000-9000-000000000010"
    )]
    public const string Returns = "/sales/returns";

    // Movido desde FinanceModule (antes /finance/receivables en el grupo "finance" separado) —
    // mismo Id/ruta/permiso, ahora dentro de Ventas → Operación.
    [NavItem(
        "Cuentas por cobrar",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.finance.receivables",
        SortOrder = 30,
        Id = "f6000000-0000-4000-9000-000000000001",
        ParentId = "e4000000-0000-4000-9000-000000000010"
    )]
    public const string Receivables = "/finance/receivables";

    [NavItem(
        "Electronic Documents Monitor",
        Permission = ElectronicDocumentsPermissions.View,
        LabelKey = "app.nav.item.electronicDocuments.monitor",
        SortOrder = 40,
        ParentId = "e4000000-0000-4000-9000-000000000010"
    )]
    public const string ElectronicDocumentsMonitor = "/electronic-documents/monitor";

    // ── Configuración ────────────────────────────────────────────────
    [NavItem(
        "Configuración",
        LabelKey = "app.nav.item.sales.configuration",
        SortOrder = 20,
        Id = "e4000000-0000-4000-9000-000000000020",
        PermissionsAnyCsv = SalesPermissions.View + "," + OperationalPreferencesPermissions.View
    )]
    public const string ConfigurationGroup = "/sales/configuration-group";

    [NavItem(
        "Métodos de Pago",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.sales.paymentMethods",
        SortOrder = 10,
        Id = "d1000000-0000-4000-9000-000000000002",
        ParentId = "e4000000-0000-4000-9000-000000000020"
    )]
    public const string PaymentMethods = "/sales/payment-methods";

    // Enlace contextual al tab "salesPos" de la pantalla única de Preferencias Operativas
    // (/settings/operations) — no duplica la pantalla, solo la referencia con deep-link.
    [NavItem(
        "Preferencias de Ventas/POS",
        Permission = OperationalPreferencesPermissions.View,
        LabelKey = "app.nav.item.sales.posPreferences",
        SortOrder = 20,
        Id = "e4000000-0000-4000-9000-000000000021",
        ParentId = "e4000000-0000-4000-9000-000000000020"
    )]
    public const string PosPreferences = "/settings/operations?tab=salesPos";

    // ── Reportes ─────────────────────────────────────────────────────
    [NavItem(
        "Reportes",
        LabelKey = "app.nav.item.sales.reports",
        SortOrder = 30,
        Id = "e4000000-0000-4000-9000-000000000030",
        PermissionsAnyCsv = SalesPermissions.View
    )]
    public const string ReportsGroup = "/sales/reports-group";

    // Movido desde ReportsModule (antes /reportes/ventas en el grupo "reports" separado) —
    // mismo Id/ruta/permiso, ahora dentro de Ventas → Reportes.
    [NavItem(
        "Reporte de Ventas",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.reportes.ventas",
        SortOrder = 10,
        Id = "f7000000-0000-4000-9000-000000000001",
        ParentId = "e4000000-0000-4000-9000-000000000030"
    )]
    public const string SalesReport = "/reportes/ventas";
}
