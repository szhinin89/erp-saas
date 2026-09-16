using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// NAVIGATION-OPERATING-CYCLES-03: módulo original — concentraba el ciclo proveedor completo,
// fusionando MasterDataModule.Suppliers + PurchasesModule + ExpensesModule + PayablesModule.
//
// MAPA-MENU-ERP-SSOT-01: Compras y Gastos se separan de vuelta a sus propios módulos de nivel
// superior (PurchasesModule.cs / ExpensesModule.cs) — el árbol objetivo de este ticket los lista
// como tiles independientes del launcher, no como categorías anidadas dentro de "Proveedores".
// Mismos Ids/rutas/permisos en los archivos nuevos, solo cambia a qué [Module] pertenecen. Este
// módulo queda con: catálogo de Proveedores, Cuentas por pagar (+ Pagos a proveedores + Créditos
// de proveedor, reagrupados aquí — antes Créditos de proveedor vivía dentro de "Compras") y
// Condiciones comerciales (movida desde SettingsModule — Pago/Crédito son catálogos usados en el
// ciclo de compra, y el árbol objetivo las pide explícitamente bajo Proveedores).
//
// URLS-MENU-ALIGNMENT-01: Proveedores/Créditos/Condiciones realineados al prefijo /suppliers/*
// (antes heredaban /masterdata/*, /finance/* y /master/* de sus módulos de origen) — mismos
// Ids/páginas/permisos, solo cambia la URL visible. Cuentas por pagar/Pagos a proveedores no
// cambian — fuera del alcance explícito de este ticket (ya no llevaban prefijo incoherente).
[Module("suppliers", Icon = "🏢", SortOrder = 20)]
public static class SuppliersModule
{
    // NAV-HIERARCHY-UNIFY-01: contenedor "Gestión de proveedores" — antes Proveedores quedaba
    // suelto directamente bajo el módulo (Nivel 1). Todo ítem de primer nivel del módulo debe
    // ser una categoría (Nivel 2); Proveedores pasa a ser su único hijo.
    [NavItem(
        "Gestión de proveedores",
        LabelKey = "app.nav.item.suppliers.managementGroup",
        SortOrder = 5,
        Id = "6093d90b-221b-41e0-8d6d-25391ec5d4e6",
        PermissionsAnyCsv = MasterDataPermissions.BusinessPartnersView
    )]
    public const string ManagementGroup = "/suppliers/management-group";

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
    public const string Suppliers = "/suppliers";

    // ── Cuentas por pagar (movido desde PayablesModule) ────────────────────────────────
    // NAV-HIERARCHY-UNIFY-01: Cuentas por pagar NO pertenece a Compras ni a Gastos — categoría
    // propia. Créditos de proveedor se reagrupa aquí (MAPA-MENU-ERP-SSOT-01): antes vivía dentro
    // de "Compras", pero es una relación financiera con el proveedor (igual que Cuentas por pagar/
    // Pagos), no un documento de compra.
    [NavItem(
        "Cuentas por pagar",
        LabelKey = "app.nav.item.suppliers.payablesGroup",
        SortOrder = 10,
        Id = "40aa3390-e353-4cd4-92fb-3b4f01bee262",
        PermissionsAnyCsv = PayablesPermissions.View + "," + SupplierPaymentsPermissions.View + ","
            + FinancePermissions.View
    )]
    public const string PayablesGroup = "/payables/group";

    [NavItem(
        "Cuentas por pagar",
        Permission = PayablesPermissions.View,
        LabelKey = "app.nav.item.payables.list",
        SortOrder = 10,
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
        SortOrder = 20,
        Id = "c9000000-0000-4000-9000-000000000002",
        ParentId = "40aa3390-e353-4cd4-92fb-3b4f01bee262",
        RelatedActionPermissionsCsv = SupplierPaymentsPermissions.Create + ","
            + SupplierPaymentsPermissions.Reverse
    )]
    public const string SupplierPayments = "/supplier-payments";

    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: Update (aplicar/reembolsar crédito —
    // ApplySupplierCreditModal.tsx/RegisterSupplierCreditRefundModal.tsx, SupplierCreditController)
    // es la única acción de escritura real de esta pantalla y no estaba en el catálogo asignable.
    [NavItem(
        "Créditos de proveedor",
        Permission = FinancePermissions.View,
        LabelKey = "app.nav.item.finance.supplierCredits",
        SortOrder = 30,
        Id = "f6000000-0000-4000-9000-000000000003",
        ParentId = "40aa3390-e353-4cd4-92fb-3b4f01bee262",
        RelatedActionPermissionsCsv = FinancePermissions.Update
    )]
    public const string SupplierCredits = "/suppliers/credits";

    // MAPA-MENU-ERP-SSOT-01: movido desde SettingsModule ("Configuración > Condiciones
    // comerciales") — el árbol objetivo pide "Condiciones pago/crédito" bajo Proveedores. Mismos
    // Ids/rutas/permisos, solo cambia su ubicación en el menú.
    [NavItem(
        "Condiciones comerciales",
        LabelKey = "app.nav.item.settings.commercialTermsGroup",
        SortOrder = 40,
        Id = "3ac9c729-c29b-4e88-a1eb-b0d8073828c2",
        PermissionsAnyCsv = MasterDataPermissions.PaymentTermsView + "," + FinancePermissions.View
    )]
    public const string CommercialTermsGroup = "/suppliers/commercial-terms-group";

    [NavItem(
        "Condiciones de Pago",
        Permission = MasterDataPermissions.PaymentTermsView,
        LabelKey = "app.nav.item.masterdata.paymentTerms",
        SortOrder = 10,
        Id = "a1000000-0000-4000-9000-000000000103",
        ParentId = "3ac9c729-c29b-4e88-a1eb-b0d8073828c2",
        RelatedActionPermissionsCsv = MasterDataPermissions.PaymentTermsManage
    )]
    public const string PaymentTermsCustomer = "/suppliers/payment-terms";

    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: Create/Update (CreditTermsPage.tsx →
    // creditTermService.create/update/enable/disable, CreditTermsController) son acciones reales
    // de esta pantalla y no estaban en el catálogo asignable.
    [NavItem(
        "Condiciones de Crédito",
        Permission = FinancePermissions.View,
        LabelKey = "app.nav.item.finance.creditTerms",
        SortOrder = 20,
        Id = "b2000000-0000-4000-9000-000000000001",
        ParentId = "3ac9c729-c29b-4e88-a1eb-b0d8073828c2",
        RelatedActionPermissionsCsv = FinancePermissions.Create + "," + FinancePermissions.Update
    )]
    public const string CreditTerms = "/suppliers/credit-terms";

    // DESTINOS-CONTABLES-COBROS-VENTAS-01: NavItem "Formas de cobro" (antes aquí, movido desde
    // SalesModule por PAYMENT-METHOD-ACCOUNT-UI-NAV-01) reubicado a
    // AccountingModule.SalesCollectionDestinations (Contabilidad > Configuración > Destinos
    // contables > Cobros de ventas) — mismo Id (d1000000-0000-4000-9000-000000000002), misma
    // ruta/página/permisos, solo cambia su ubicación en el menú. Una sola entrada de menú, no dos.

    // NOTA (MAPA-MENU-ERP-SSOT-01): no se agrega grupo "Reportes" a Proveedores — no existe una
    // pantalla de reporte específica de proveedores (solo /reportes/compras, que ahora vive en
    // PurchasesModule). Regla del ticket: no crear pantallas nuevas.
}
