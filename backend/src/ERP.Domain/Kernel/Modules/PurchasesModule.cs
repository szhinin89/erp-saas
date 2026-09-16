using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MAPA-MENU-ERP-SSOT-01: separado de vuelta desde SuppliersModule (donde vivía como categoría
// "Compras" desde NAVIGATION-OPERATING-CYCLES-03) — el árbol objetivo de este ticket lo lista
// como tile propio del launcher, hermano de Proveedores/Gastos, no anidado dentro de Proveedores.
// Mismos Ids/rutas/permisos que tenía en SuppliersModule — solo cambia a qué [Module] pertenece.
[Module("purchases", Icon = "🧾", SortOrder = 30)]
public static class PurchasesModule
{
    [NavItem(
        "Compras",
        LabelKey = "app.nav.item.purchases.operation",
        SortOrder = 10,
        Id = "e3000000-0000-4000-9000-000000000010",
        PermissionsAnyCsv = PurchasePermissions.View + "," + FinancePermissions.View
    )]
    public const string PurchasesGroup = "/purchases/operation-group";

    // ZH-MENU-TAXONOMY-STANDARD-01: renombrado de "Compras" a "Facturas de compra" — evita
    // repetir el nombre del grupo contenedor ("Compras") en su pantalla principal (mismo Id/ruta/
    // permiso, solo cambia el label i18n).
    [NavItem(
        "Facturas de compra",
        Permission = PurchasePermissions.View,
        LabelKey = "app.nav.item.purchases.invoices",
        SortOrder = 10,
        Id = "c1000000-0000-4000-9000-000000000001",
        ParentId = "e3000000-0000-4000-9000-000000000010",
        RelatedActionPermissionsCsv = PurchasePermissions.Create + "," + PurchasePermissions.Update
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

    // PURCHASE-RETURNS-REMOVE-FROM-MAIN-MENU-01 — la devolución de mercadería es un flujo dentro
    // de "Notas de crédito de compra" (tipo Return), no un módulo principal para el usuario; ya no
    // es un ítem de menú (se quita el atributo [NavItem], nunca la ruta/lógica de PurchaseReturn:
    // /purchases/returns y /purchases/returns/{id} siguen existiendo y funcionando como rutas
    // técnicas/secundarias, ej. el botón "Ver devolución vinculada" desde el detalle de NC).
    public const string Returns = "/purchases/returns";

    // PURCHASE-CREDIT-NOTE-ENTRY-SCREEN-DUAL-MODE-01 — antes solo se llegaba a
    // PurchaseCreditNoteFormPage desde Recepción XML/SRI o desde una factura/devolución puntual,
    // sin punto de entrada propio en el menú. Mismo patrón que "Devoluciones de compra": el menú
    // apunta al LISTADO (PurchaseCreditNoteListPage), nunca directo a /new — "Nueva" es un botón
    // dentro de esa pantalla que abre el mismo formulario en modo manual.
    [NavItem(
        "Notas de crédito de compra",
        Permission = PurchasePermissions.View,
        LabelKey = "app.nav.item.purchases.creditNotes",
        SortOrder = 30,
        Id = "c1000000-0000-4000-9000-000000000004",
        ParentId = "e3000000-0000-4000-9000-000000000010"
    )]
    public const string CreditNotes = "/purchases/credit-notes";

    [NavItem(
        "Configuración",
        LabelKey = "app.nav.item.purchases.configuration",
        SortOrder = 20,
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

    [NavItem(
        "Reportes",
        LabelKey = "app.nav.item.purchases.reports",
        SortOrder = 30,
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
