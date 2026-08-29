using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// ACCOUNTING-NAV-VISIBILITY-FIX-10C: el módulo Contabilidad tenía [AppFeature] en
// AccountingController (alimenta únicamente el catálogo de permisos vía
// AppFeatureDiscoveryService/app_features) pero nunca tuvo [Module]/[NavItem] aquí — el menú
// lateral real (GET /api/v1/me/menu) se arma exclusivamente desde KernelRegistry.Navigation
// (sincronizado a ui_nav_groups/ui_nav_items por NavigationSyncService en cada arranque), un
// mecanismo distinto (ver comentario ya existente en SettingsModule.cs). Sin ese registro,
// "Contabilidad" nunca podía aparecer en el menú real aunque la ruta React y el permiso
// existieran.
//
// ACCOUNTING-MODULE-MENU-STRUCTURE-FIX-10D: el fix anterior (10C) registró un único NavItem
// "Contabilidad" apuntando al hub de tarjetas /accounting (ACCOUNTING-MODULE-NAV-UX-10B) — en
// revisión manual se confirmó que esa NO es la UX esperada: el resto del ERP expone cada
// pantalla real como ítem de menú independiente (mismo criterio ya usado por ProductsModule:
// varios ítems planos bajo un mismo módulo, sin contenedor "Operación"/"Configuración" cuando
// no hace falta, a diferencia de CajaModule). Se reemplaza el ítem único por tres ítems planos
// — Asientos contables/Plan de cuentas/Reportes — cada uno apuntando directo a su ruta real ya
// implementada. Los tres comparten AccountingPermissions.View porque es el ÚNICO permiso que
// protege hoy los tres endpoints reales (journal-entries*/accounts*/reports*, ver
// AccountingController/AccountingReportsController) — no se inventan permisos granulares
// (accounting.journalEntries.view/etc.) sin una autorización de backend real detrás; inventarlos
// habría sido un permiso hueco, sin ningún efecto de seguridad. Sin tocar Posting Engine,
// reportes, plan de cuentas, asientos, ni crear pantallas de Configuración contable/Períodos
// contables (siguen sin CRUD real en frontend — no se agregan al menú, restricción explícita del
// ticket).
//
// ACCOUNTING-NAVIGATION-CANONICAL-AUDIT-11C: el hub /accounting (AccountingHubPage.tsx,
// mencionado como landing opcional en el comentario original de 10D) SÍ se eliminó — duplicaba
// la navegación del menú sin aportar nada que las 3 pantallas principales no dieran ya. La ruta
// /accounting sigue existiendo solo como redirect técnico a /accounting/journal-entries (frontend,
// catalogRoutes.tsx) — nunca como NavItem aquí.
//
// ACCOUNTING-NAV-ORPHAN-ROUTES-AUDIT-11D: auditado — no hay ninguna pantalla real de
// Configuración contable/Reglas contables/Períodos contables en el frontend (accountingApi.ts no
// consume posting-rules*/accounting-periods* en absoluto); esos endpoints de backend existen sin
// UI, así que no calificaban como "pantalla real" para agregar al menú (regla 4/8 del ticket).
//
// ACCOUNTING-POSTING-RULES-UI-12: "Reglas contables" pasó a ser pantalla real de solo lectura
// (PostingRulesPage.tsx, consume GET /api/v1/accounting/posting-rules) — deja de aplicar la
// exclusión de 11D para este caso puntual; se agrega como 4to NavItem plano, mismo criterio que
// los 3 anteriores (AccountingPermissions.View, sin permiso granular nuevo). Accounting
// Periods/Configuración contable siguen sin UI real, así que siguen fuera del menú.
[Module("accounting", Icon = "📒", SortOrder = 46)]
public static class AccountingModule
{
    // NAV-HIERARCHY-UNIFY-01: contenedores de categoría — ningún ítem plano bajo el módulo.
    [NavItem(
        "Asientos",
        LabelKey = "app.nav.item.accounting.journalGroup",
        SortOrder = 10,
        Id = "405b951f-84c9-4520-a7be-53d1e52b0cf4",
        PermissionsAnyCsv = AccountingPermissions.View
    )]
    public const string JournalGroup = "/accounting/journal-entries/group";

    [NavItem(
        "Asientos contables",
        Permission = AccountingPermissions.View,
        LabelKey = "app.nav.item.accounting.journalEntries",
        SortOrder = 10,
        Id = "ac000000-0000-4000-9000-000000000001",
        ParentId = "405b951f-84c9-4520-a7be-53d1e52b0cf4"
    )]
    public const string JournalEntries = "/accounting/journal-entries";

    [NavItem(
        "Plan contable",
        LabelKey = "app.nav.item.accounting.chartGroup",
        SortOrder = 20,
        Id = "72e69e8c-e34d-4ee4-b3ff-3568acc7d899",
        PermissionsAnyCsv = AccountingPermissions.View
    )]
    public const string ChartGroup = "/accounting/chart-of-accounts/group";

    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: a diferencia de Asientos contables/Reportes (solo
    // lectura) y Reglas contables (solo lectura por decisión de 12), Plan de cuentas SÍ tiene
    // Crear/Actualizar/Deshabilitar reales (ChartOfAccountsPage.tsx → accountingApi.createAccount/
    // updateAccount, AccountingController.CreateAccount/UpdateAccount/DisableAccount) — no estaban
    // en el catálogo asignable pese a exigirlos el endpoint.
    [NavItem(
        "Plan de cuentas",
        Permission = AccountingPermissions.View,
        LabelKey = "app.nav.item.accounting.chartOfAccounts",
        SortOrder = 20,
        Id = "ac000000-0000-4000-9000-000000000002",
        ParentId = "72e69e8c-e34d-4ee4-b3ff-3568acc7d899",
        RelatedActionPermissionsCsv = AccountingPermissions.Create + ","
            + AccountingPermissions.Update + "," + AccountingPermissions.Delete
    )]
    public const string ChartOfAccounts = "/accounting/chart-of-accounts";

    [NavItem(
        "Reglas contables",
        Permission = AccountingPermissions.View,
        LabelKey = "app.nav.item.accounting.postingRules",
        SortOrder = 25,
        Id = "ac000000-0000-4000-9000-000000000004",
        ParentId = "72e69e8c-e34d-4ee4-b3ff-3568acc7d899"
    )]
    public const string PostingRules = "/accounting/posting-rules";

    [NavItem(
        "Reportes",
        LabelKey = "app.nav.item.accounting.reportsGroup",
        SortOrder = 30,
        Id = "5f363c9d-e97e-4a39-8bf7-1599915e26a1",
        PermissionsAnyCsv = AccountingPermissions.View
    )]
    public const string ReportsGroup = "/accounting/reports/group";

    [NavItem(
        "Reportes",
        Permission = AccountingPermissions.View,
        LabelKey = "app.nav.item.accounting.reports",
        SortOrder = 30,
        Id = "ac000000-0000-4000-9000-000000000003",
        ParentId = "5f363c9d-e97e-4a39-8bf7-1599915e26a1"
    )]
    public const string Reports = "/accounting/reports";
}
