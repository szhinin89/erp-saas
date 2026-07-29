using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("sales", Icon = "💰", SortOrder = 40)]
public static class SalesModule
{
    // ── Contenedor: Documentos ─────────────────────────────────────
    [NavItem(
        "Documentos",
        LabelKey = "app.nav.item.sales.invoices",
        SortOrder = 10,
        Id = "e4000000-0000-4000-9000-000000000001",
        PermissionsAnyCsv = SalesPermissions.View
    )]
    public const string DocumentsGroup = "/sales/documents-group";

    [NavItem(
        "Facturas de Venta",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.sales.invoices",
        SortOrder = 10,
        Id = "d1000000-0000-4000-9000-000000000001",
        ParentId = "e4000000-0000-4000-9000-000000000001"
    )]
    public const string Invoices = "/sales";

    // ── Contenedor: Cobros ─────────────────────────────────────────
    [NavItem(
        "Cobros",
        LabelKey = "app.nav.item.sales.paymentMethods",
        SortOrder = 20,
        Id = "e4000000-0000-4000-9000-000000000002",
        PermissionsAnyCsv = SalesPermissions.View
    )]
    public const string PaymentsGroup = "/sales/payments-group";

    [NavItem(
        "Métodos de Pago",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.sales.paymentMethods",
        SortOrder = 10,
        Id = "d1000000-0000-4000-9000-000000000002",
        ParentId = "e4000000-0000-4000-9000-000000000002"
    )]
    public const string PaymentMethods = "/sales/payment-methods";

    // ── Contenedor: Facturación Electrónica ─────────────────────────
    [NavItem(
        "Facturación Electrónica",
        LabelKey = "app.nav.item.settings.electronicInvoicing",
        SortOrder = 30,
        Id = "e4000000-0000-4000-9000-000000000003",
        PermissionsAnyCsv = ElectronicInvoicingPermissions.View
    )]
    public const string ElectronicGroup = "/sales/electronic-group";

    [NavItem(
        "Electronic Invoicing",
        Permission = ElectronicInvoicingPermissions.View,
        LabelKey = "app.nav.item.settings.electronicInvoicing",
        SortOrder = 10,
        ParentId = "e4000000-0000-4000-9000-000000000003"
    )]
    public const string ElectronicInvoicing = "/settings/electronic-invoicing";

    [NavItem(
        "Electronic Documents Monitor",
        Permission = ElectronicDocumentsPermissions.View,
        LabelKey = "app.nav.item.electronicDocuments.monitor",
        SortOrder = 20,
        ParentId = "e4000000-0000-4000-9000-000000000003"
    )]
    public const string ElectronicDocumentsMonitor = "/electronic-documents/monitor";
}
