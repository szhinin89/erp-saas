using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MAPA-MENU-ERP-SSOT-01: separado de vuelta desde SuppliersModule (donde vivía como categoría
// "Gastos" desde NAVIGATION-OPERATING-CYCLES-03) — el árbol objetivo de este ticket lo lista como
// tile propio del launcher, hermano de Proveedores/Compras, no anidado dentro de Proveedores.
// Mismos Ids/rutas/permisos que tenía en SuppliersModule — solo cambia a qué [Module] pertenece.
[Module("expenses", Icon = "💸", SortOrder = 40)]
public static class ExpensesModule
{
    [NavItem(
        "Gastos",
        LabelKey = "app.nav.item.suppliers.expensesGroup",
        SortOrder = 10,
        Id = "ca6fa276-a8bc-4dc7-b207-7c37d57341ad",
        PermissionsAnyCsv = ExpensePermissions.DocumentsView + "," + ExpensePermissions.CatalogView
    )]
    public const string ExpensesGroup = "/expenses/group";

    [NavItem(
        "Documentos de Gastos",
        Permission = ExpensePermissions.DocumentsView,
        LabelKey = "app.nav.item.expenses.documents",
        SortOrder = 10,
        Id = "e5000000-0000-4000-9000-000000000002",
        ParentId = "ca6fa276-a8bc-4dc7-b207-7c37d57341ad",
        RelatedActionPermissionsCsv = ExpensePermissions.DocumentsCreate + ","
            + ExpensePermissions.DocumentsUpdate + "," + ExpensePermissions.DocumentsConfirm
            + "," + ExpensePermissions.DocumentsCancel
    )]
    public const string ExpenseDocuments = "/expenses/documents";

    [NavItem(
        "Catalogo de Gastos",
        Permission = ExpensePermissions.CatalogView,
        LabelKey = "app.nav.item.expenses.catalog",
        SortOrder = 20,
        Id = "e5000000-0000-4000-9000-000000000001",
        ParentId = "ca6fa276-a8bc-4dc7-b207-7c37d57341ad",
        RelatedActionPermissionsCsv = ExpensePermissions.CatalogCreate + ","
            + ExpensePermissions.CatalogUpdate + "," + ExpensePermissions.CatalogActivate + ","
            + ExpensePermissions.CatalogDeactivate
    )]
    public const string ExpenseCatalog = "/expenses/categories";

    // NOTA (MAPA-MENU-ERP-SSOT-01): no se agrega grupo "Reportes" — no existe una pantalla de
    // reporte de gastos todavía. Regla del ticket: no crear pantallas nuevas.
}
