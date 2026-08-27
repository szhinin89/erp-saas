using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("expenses", Icon = "ReceiptText", SortOrder = 47)]
public static class ExpensesModule
{
    [NavItem(
        "Documentos de Gastos",
        Permission = ExpensePermissions.DocumentsView,
        LabelKey = "app.nav.item.expenses.documents",
        SortOrder = 5,
        Id = "e5000000-0000-4000-9000-000000000002"
    )]
    public const string Documents = "/expenses/documents";

    [NavItem(
        "Catalogo de Gastos",
        Permission = ExpensePermissions.CatalogView,
        LabelKey = "app.nav.item.expenses.catalog",
        SortOrder = 10,
        Id = "e5000000-0000-4000-9000-000000000001"
    )]
    public const string Catalog = "/expenses/categories";
}
