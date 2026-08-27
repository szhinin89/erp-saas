namespace ERP.Domain.Kernel.Permissions;

public static class ExpensePermissions
{
    public const string CatalogView = "expenses.catalog.view";
    public const string CatalogCreate = "expenses.catalog.create";
    public const string CatalogUpdate = "expenses.catalog.update";
    public const string CatalogActivate = "expenses.catalog.activate";
    public const string CatalogDeactivate = "expenses.catalog.deactivate";

    public const string DocumentsView = "expenses.documents.view";
    public const string DocumentsCreate = "expenses.documents.create";
    public const string DocumentsUpdate = "expenses.documents.update";
    public const string DocumentsConfirm = "expenses.documents.confirm";
}
