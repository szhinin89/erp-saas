using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("finance", Icon = "💳", SortOrder = 46)]
public static class FinanceModule
{
    // Permission alineado con SalesReceivablesController (perm:sales.view), no FinancePermissions.View:
    // el NavItem debe reflejar el permiso real que exige la API que consume esta pantalla.
    [NavItem(
        "Cuentas por cobrar",
        Permission = SalesPermissions.View,
        LabelKey = "app.nav.item.finance.receivables",
        SortOrder = 10,
        Id = "f6000000-0000-4000-9000-000000000001"
    )]
    public const string Receivables = "/finance/receivables";

    // Permission alineado con PurchasePayablesController (perm:purchases.view), no FinancePermissions.View:
    // el NavItem debe reflejar el permiso real que exige la API que consume esta pantalla.
    [NavItem(
        "Cuentas por pagar",
        Permission = PurchasePermissions.View,
        LabelKey = "app.nav.item.finance.payables",
        SortOrder = 20,
        Id = "f6000000-0000-4000-9000-000000000002"
    )]
    public const string Payables = "/finance/payables";

    [NavItem(
        "Créditos de proveedor",
        Permission = FinancePermissions.View,
        LabelKey = "app.nav.item.finance.supplierCredits",
        SortOrder = 30,
        Id = "f6000000-0000-4000-9000-000000000003"
    )]
    public const string SupplierCredits = "/finance/supplier-credits";
}
