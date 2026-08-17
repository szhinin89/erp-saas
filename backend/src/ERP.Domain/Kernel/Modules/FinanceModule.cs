using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("finance", Icon = "💳", SortOrder = 46)]
public static class FinanceModule
{
    [NavItem(
        "Cuentas por cobrar",
        Permission = FinancePermissions.View,
        LabelKey = "app.nav.item.finance.receivables",
        SortOrder = 10,
        Id = "f6000000-0000-4000-9000-000000000001"
    )]
    public const string Receivables = "/finance/receivables";

    [NavItem(
        "Cuentas por pagar",
        Permission = FinancePermissions.View,
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
