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
// habría sido un permiso hueco, sin ningún efecto de seguridad. El hub /accounting
// (AccountingHubPage.tsx) NO se elimina — sigue existiendo como landing opcional (accesible por
// URL directa o desde cualquiera de las 3 páginas vía su botón "Contabilidad"), pero deja de ser
// la única forma de navegar el módulo: el menú ahora expone las 3 pantallas directamente. Sin
// tocar Posting Engine, reportes, plan de cuentas, asientos, ni crear pantallas de Configuración
// contable/Períodos contables (siguen sin CRUD real — no se agregan al menú, restricción
// explícita del ticket).
[Module("accounting", Icon = "📒", SortOrder = 46)]
public static class AccountingModule
{
    [NavItem(
        "Asientos contables",
        Permission = AccountingPermissions.View,
        LabelKey = "app.nav.item.accounting.journalEntries",
        SortOrder = 10,
        Id = "ac000000-0000-4000-9000-000000000001"
    )]
    public const string JournalEntries = "/accounting/journal-entries";

    [NavItem(
        "Plan de cuentas",
        Permission = AccountingPermissions.View,
        LabelKey = "app.nav.item.accounting.chartOfAccounts",
        SortOrder = 20,
        Id = "ac000000-0000-4000-9000-000000000002"
    )]
    public const string ChartOfAccounts = "/accounting/chart-of-accounts";

    [NavItem(
        "Reportes",
        Permission = AccountingPermissions.View,
        LabelKey = "app.nav.item.accounting.reports",
        SortOrder = 30,
        Id = "ac000000-0000-4000-9000-000000000003"
    )]
    public const string Reports = "/accounting/reports";
}
