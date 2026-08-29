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
    // NAV-HIERARCHY-UNIFY-01: contenedor "Gestión de proveedores" — antes Proveedores quedaba
    // suelto directamente bajo el módulo (Nivel 1). Todo ítem de primer nivel del módulo debe
    // ser una categoría (Nivel 2); Proveedores pasa a ser su único hijo. Mismo patrón ya usado
    // por "Compras"/"Configuración"/"Reportes" en este mismo archivo.
    [NavItem(
        "Gestión de proveedores",
        LabelKey = "app.nav.item.suppliers.managementGroup",
        SortOrder = 5,
        Id = "6093d90b-221b-41e0-8d6d-25391ec5d4e6",
        PermissionsAnyCsv = MasterDataPermissions.BusinessPartnersView
    )]
    public const string ManagementGroup = "/masterdata/suppliers/management-group";

    // Movido desde MasterDataModule — mismo Id/ruta/permiso.
    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: Create/Update/Disable/ConfigureCompany
    // (useMasterDataSuppliersPage.ts canCreate/canUpdate/canDisable/canConfigure,
    // MasterDataBusinessPartnerDetailPage.tsx canUpdate/canDisable) son acciones reales de esta
    // pantalla y no estaban en el catálogo asignable — ningún perfil no-Admin podía recibirlas.
    [NavItem(
        "Proveedores",
        Permission = MasterDataPermissions.BusinessPartnersView,
        LabelKey = "app.nav.item.masterdata.suppliers",
        SortOrder = 5,
        Id = "a1000000-0000-4000-9000-000000000102",
        ParentId = "6093d90b-221b-41e0-8d6d-25391ec5d4e6",
        RelatedActionPermissionsCsv = MasterDataPermissions.BusinessPartnersCreate + ","
            + MasterDataPermissions.BusinessPartnersUpdate + ","
            + MasterDataPermissions.BusinessPartnersDisable + ","
            + MasterDataPermissions.BusinessPartnersConfigureCompany
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

    [NavItem(
        "Devoluciones de compra",
        Permission = PurchasePermissions.View,
        LabelKey = "app.nav.item.purchases.returns",
        SortOrder = 30,
        Id = "c1000000-0000-4000-9000-000000000003",
        ParentId = "e3000000-0000-4000-9000-000000000010"
    )]
    public const string Returns = "/purchases/returns";

    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: Update (aplicar/reembolsar crédito —
    // ApplySupplierCreditModal.tsx/RegisterSupplierCreditRefundModal.tsx, SupplierCreditController)
    // es la única acción de escritura real de esta pantalla y no estaba en el catálogo asignable.
    [NavItem(
        "Créditos de proveedor",
        Permission = FinancePermissions.View,
        LabelKey = "app.nav.item.finance.supplierCredits",
        SortOrder = 50,
        Id = "f6000000-0000-4000-9000-000000000003",
        ParentId = "e3000000-0000-4000-9000-000000000010",
        RelatedActionPermissionsCsv = FinancePermissions.Update
    )]
    public const string SupplierCredits = "/finance/supplier-credits";

    // ── Gastos (movido desde ExpensesModule, ítems planos sin cambios) ─────────────────
    // NAV-HIERARCHY-UNIFY-01: Gastos NO pertenece a Compras — categoría propia, hermana de
    // "Compras", no anidada dentro de ella.
    [NavItem(
        "Gastos",
        LabelKey = "app.nav.item.suppliers.expensesGroup",
        SortOrder = 20,
        Id = "ca6fa276-a8bc-4dc7-b207-7c37d57341ad",
        PermissionsAnyCsv = ExpensePermissions.DocumentsView + "," + ExpensePermissions.CatalogView
    )]
    public const string ExpensesGroup = "/expenses/group";

    [NavItem(
        "Documentos de Gastos",
        Permission = ExpensePermissions.DocumentsView,
        LabelKey = "app.nav.item.expenses.documents",
        SortOrder = 20,
        Id = "e5000000-0000-4000-9000-000000000002",
        ParentId = "ca6fa276-a8bc-4dc7-b207-7c37d57341ad",
        RelatedActionPermissionsCsv = ExpensePermissions.DocumentsCreate + ","
            + ExpensePermissions.DocumentsUpdate + "," + ExpensePermissions.DocumentsConfirm
    )]
    public const string ExpenseDocuments = "/expenses/documents";

    [NavItem(
        "Catalogo de Gastos",
        Permission = ExpensePermissions.CatalogView,
        LabelKey = "app.nav.item.expenses.catalog",
        SortOrder = 21,
        Id = "e5000000-0000-4000-9000-000000000001",
        ParentId = "ca6fa276-a8bc-4dc7-b207-7c37d57341ad",
        RelatedActionPermissionsCsv = ExpensePermissions.CatalogCreate + ","
            + ExpensePermissions.CatalogUpdate + "," + ExpensePermissions.CatalogActivate + ","
            + ExpensePermissions.CatalogDeactivate
    )]
    public const string ExpenseCatalog = "/expenses/categories";

    // ── Cuentas por pagar (movido desde PayablesModule) ────────────────────────────────
    // NAV-HIERARCHY-UNIFY-01: Cuentas por pagar NO pertenece a Compras ni a Gastos — categoría
    // propia, hermana de ambas.
    [NavItem(
        "Cuentas por pagar",
        LabelKey = "app.nav.item.suppliers.payablesGroup",
        SortOrder = 30,
        Id = "40aa3390-e353-4cd4-92fb-3b4f01bee262",
        PermissionsAnyCsv = PayablesPermissions.View + "," + SupplierPaymentsPermissions.View
    )]
    public const string PayablesGroup = "/payables/group";

    [NavItem(
        "Cuentas por pagar",
        Permission = PayablesPermissions.View,
        LabelKey = "app.nav.item.payables.list",
        SortOrder = 30,
        Id = "c9000000-0000-4000-9000-000000000001",
        ParentId = "40aa3390-e353-4cd4-92fb-3b4f01bee262"
    )]
    public const string Payables = "/payables";

    // ADMIN-PERMISSIONS-SSOT-KERNEL-02: ejemplo literal del ticket — Create/Reverse deben aparecer
    // como acciones relacionadas junto al permiso de acceso (View) en Asignación de permisos.
    [NavItem(
        "Pagos a proveedores",
        Permission = SupplierPaymentsPermissions.View,
        LabelKey = "app.nav.item.payables.supplierPayments",
        SortOrder = 40,
        Id = "c9000000-0000-4000-9000-000000000002",
        ParentId = "40aa3390-e353-4cd4-92fb-3b4f01bee262",
        RelatedActionPermissionsCsv = SupplierPaymentsPermissions.Create + ","
            + SupplierPaymentsPermissions.Reverse
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
