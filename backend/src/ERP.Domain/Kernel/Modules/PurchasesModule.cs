using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("purchases", Icon = "🛒", SortOrder = 30)]
public static class PurchasesModule
{
    // ── Contenedor: Documentos ─────────────────────────────────────
    [NavItem(
        "Documentos",
        LabelKey = "app.nav.item.purchases.invoices",
        SortOrder = 10,
        Id = "e3000000-0000-4000-9000-000000000001",
        PermissionsAnyCsv = PurchasePermissions.View
    )]
    public const string DocumentsGroup = "/purchases/documents-group";

    [NavItem(
        "Compras",
        Permission = PurchasePermissions.View,
        LabelKey = "app.nav.item.purchases.invoices",
        SortOrder = 10,
        Id = "c1000000-0000-4000-9000-000000000001",
        ParentId = "e3000000-0000-4000-9000-000000000001"
    )]
    public const string Invoices = "/purchases";

    [NavItem(
        "Recepción electrónica",
        Permission = PurchasePermissions.View,
        LabelKey = "app.nav.item.purchases.reception",
        SortOrder = 20,
        Id = "c1000000-0000-4000-9000-000000000002",
        ParentId = "e3000000-0000-4000-9000-000000000001"
    )]
    public const string Reception = "/purchases/reception";

    [NavItem(
        "Devoluciones de compra",
        Permission = PurchasePermissions.View,
        LabelKey = "app.nav.item.purchases.returns",
        SortOrder = 30,
        Id = "c1000000-0000-4000-9000-000000000003",
        ParentId = "e3000000-0000-4000-9000-000000000001"
    )]
    public const string Returns = "/purchases/returns";
}
