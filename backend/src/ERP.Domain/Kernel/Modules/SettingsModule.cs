using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// MAPA-MENU-ERP-SSOT-01: Facturación electrónica se movió a SriModule (tile "SRI/Documentos
// electrónicos"), Condiciones comerciales se movió a SuppliersModule (tile "Proveedores") y
// Destinos financieros se movió a TreasuryModule (tile "Tesorería") — el árbol objetivo agrupa
// esas tres áreas fuera de Configuración general. Queda: Empresa, Documentos y flujos,
// Comunicaciones, Sistema — igual al árbol objetivo. SortOrder actualizado para su posición en el
// árbol de 12 módulos de nivel superior.
[Module("settings", Icon = "⚙", SortOrder = 110, GroupId = "f2d0ca10-0000-4000-8000-000000000008")]
public static class SettingsModule
{
    /// <summary>
    /// ADMIN-COMPANIES-REGROUP-01: movida desde AdminModule (grupo Administración) — administra
    /// datos de empresa/fiscales/branding/documentos/operación de cada Company del tenant, lo que
    /// conceptualmente es Configuración, no usuarios/perfiles/delegación/sesiones/actividad. Mismo
    /// Id/ruta/permiso que tenía en AdminModule — sin cambios de API ni de lógica de negocio.
    /// </summary>
    // NAV-HIERARCHY-UNIFY-01: contenedor "Empresa" — agrupa todos los catálogos de identidad/
    // ubicación de empresa (Mis empresas, datos de empresa, Sucursales, Establecimientos, Puntos
    // de emisión, Geografía) para que ninguno quede suelto bajo el módulo Configuración.
    // (MAPA-MENU-ERP-SSOT-01: Destinos financieros se movió a Tesorería, ver más abajo.)
    // MENU-COMPANY-HIERARCHY-FLAT-01: "Mis empresas" (multiempresa, CompaniesView) y
    // "Datos de la empresa" (empresa activa, CompanyView) son pantallas reales distintas con
    // permisos distintos; se dejan como hermanos directos bajo "Empresa" para evitar el nivel
    // redundante Configuración > Empresa > Empresas.
    [NavItem(
        "Empresa",
        LabelKey = "app.nav.item.settings.enterpriseGroup",
        SortOrder = 5,
        Id = "7eabb75d-1ccf-4a4a-a4ee-46a082a7e90d",
        PermissionsAnyCsv = SettingsPermissions.CompaniesView + "," + SettingsPermissions.CompanyView
            + "," + SettingsPermissions.BranchesView + "," + SettingsPermissions.EstablishmentsView
            + "," + SettingsPermissions.EmissionPointsView + "," + SettingsPermissions.GeographyView
    )]
    public const string EnterpriseGroup = "/settings/enterprise-group";

    [NavItem(
        "Mis empresas",
        Permission = SettingsPermissions.CompaniesView,
        LabelKey = "app.nav.item.erp.companies",
        SortOrder = 10,
        Id = "00000000-0000-4000-8000-000000000104",
        ParentId = "7eabb75d-1ccf-4a4a-a4ee-46a082a7e90d",
        RelatedActionPermissionsCsv = SettingsPermissions.CompaniesUpdate
    )]
    public const string Companies = "/companies";

    // MENU-UX-RENAME-01: label de negocio "Datos de la empresa" (antes "Company"/"Datos de
    // Empresa") — pantalla de la empresa activa (perfil/fiscal/marca), no el multiempresa.
    [NavItem(
        "Datos de la empresa",
        Permission = SettingsPermissions.CompanyView,
        LabelKey = "app.nav.item.settings.company",
        SortOrder = 20,
        Id = "00000000-0000-4000-8000-000000000101",
        ParentId = "7eabb75d-1ccf-4a4a-a4ee-46a082a7e90d"
    )]
    public const string Company = "/settings/company";

    [NavItem(
        "Branches",
        Permission = SettingsPermissions.BranchesView,
        LabelKey = "app.nav.item.settings.branches",
        SortOrder = 20,
        Id = "a1000000-0000-4000-9000-000000000005",
        ParentId = "7eabb75d-1ccf-4a4a-a4ee-46a082a7e90d",
        RelatedActionPermissionsCsv = SettingsPermissions.BranchesCreate + ","
            + SettingsPermissions.BranchesUpdate + "," + SettingsPermissions.BranchesDelete
    )]
    public const string Branches = "/settings/branches";

    [NavItem(
        "Establishments",
        Permission = SettingsPermissions.EstablishmentsView,
        LabelKey = "app.nav.item.settings.establishments",
        SortOrder = 30,
        Id = "a1000000-0000-4000-9000-000000000010",
        ParentId = "7eabb75d-1ccf-4a4a-a4ee-46a082a7e90d",
        RelatedActionPermissionsCsv = SettingsPermissions.EstablishmentsCreate + ","
            + SettingsPermissions.EstablishmentsUpdate + "," + SettingsPermissions.EstablishmentsDisable
    )]
    public const string Establishments = "/settings/establishments";

    [NavItem(
        "Emission Points",
        Permission = SettingsPermissions.EmissionPointsView,
        LabelKey = "app.nav.item.settings.emissionPoints",
        SortOrder = 40,
        Id = "a1000000-0000-4000-9000-00000000000f",
        ParentId = "7eabb75d-1ccf-4a4a-a4ee-46a082a7e90d",
        RelatedActionPermissionsCsv = SettingsPermissions.EmissionPointsCreate + ","
            + SettingsPermissions.EmissionPointsUpdate + "," + SettingsPermissions.EmissionPointsDelete
    )]
    public const string EmissionPoints = "/settings/emission-points";

    // MAPA-MENU-ERP-SSOT-01: "Destinos financieros" se movió a TreasuryModule.FinancialDestinations
    // (Tesorería > Bancos) — mismo Id (a1000000-0000-4000-9000-000000000011), nueva ruta
    // /treasury/banks/financial-destinations (antes /settings/financial-destinations, que queda
    // como redirect en el frontend).

    // NAV-HIERARCHY-UNIFY-01: contenedor "Documentos y flujos" — categoría propia, ninguna
    // pantalla real puede quedar suelta directamente bajo el módulo settings.
    [NavItem(
        "Documentos y flujos",
        LabelKey = "app.nav.item.settings.documentFlowsGroup",
        SortOrder = 60,
        Id = "8f0f7a10-0000-4000-8000-000000000001",
        PermissionsAnyCsv = SettingsPermissions.DocumentFlowsView
    )]
    public const string DocumentFlowsGroup = "/settings/document-flows/group";

    /// <summary>
    /// DOCUMENT-FLOW-POLICY-01: CÓMO se comporta cada tipo de documento por empresa. No confundir
    /// con Roles y Permisos (QUIÉN puede ejecutar cada acción).
    /// </summary>
    [NavItem(
        "Documentos y flujos",
        Permission = SettingsPermissions.DocumentFlowsView,
        LabelKey = "app.nav.item.settings.documentFlows",
        SortOrder = 10,
        Id = "a1000000-0000-4000-9000-000000000015",
        ParentId = "8f0f7a10-0000-4000-8000-000000000001",
        RelatedActionPermissionsCsv = SettingsPermissions.DocumentFlowsUpdate
    )]
    public const string DocumentFlows = "/settings/document-flows";

    // MAPA-MENU-ERP-SSOT-01: "Facturación electrónica" (grupo + pantalla) se movió a
    // SriModule (tile "SRI/Documentos electrónicos" > Configuración) — mismos Ids
    // (60530be0-ce1c-4a1c-b1e8-fa5b4256bde7 / a1000000-0000-4000-9000-000000000014), nueva ruta
    // /sri/configuration/electronic-invoicing (antes /settings/electronic-invoicing, que queda
    // como redirect en el frontend).

    // BANK-CATALOG-01: contenedor "Catálogos" — categoría propia para catálogos maestros
    // transversales de Configuración (hoy solo Bancos; preparado para futuros catálogos sin
    // crear un módulo Tesorería/Bancos todavía, fuera del alcance de este ticket).
    [NavItem(
        "Catálogos",
        LabelKey = "app.nav.item.settings.catalogsGroup",
        SortOrder = 65,
        Id = "9c7a1e20-0000-4000-8000-000000000001",
        PermissionsAnyCsv = SettingsPermissions.BanksView
    )]
    public const string CatalogsGroup = "/settings/catalogs/group";

    [NavItem(
        "Bancos",
        Permission = SettingsPermissions.BanksView,
        LabelKey = "app.nav.item.settings.banks",
        SortOrder = 10,
        Id = "9c7a1e20-0000-4000-8000-000000000002",
        ParentId = "9c7a1e20-0000-4000-8000-000000000001",
        RelatedActionPermissionsCsv = SettingsPermissions.BanksCreate + ","
            + SettingsPermissions.BanksUpdate + "," + SettingsPermissions.BanksManage
    )]
    public const string Banks = "/settings/catalogs/banks";

    // NAV-HIERARCHY-UNIFY-01: contenedor "Comunicaciones" — categoría propia.
    [NavItem(
        "Comunicaciones",
        LabelKey = "app.nav.item.settings.communicationsGroup",
        SortOrder = 70,
        Id = "70727bc1-1744-4ec8-94bf-616fc87600ec",
        PermissionsAnyCsv = CommunicationsPermissions.View
    )]
    public const string CommunicationsGroup = "/settings/communications/group";

    [NavItem(
        "Correo SMTP",
        Permission = CommunicationsPermissions.View,
        LabelKey = "app.nav.item.settings.communicationsEmail",
        SortOrder = 70,
        Id = "a1000000-0000-4000-9000-000000000012",
        ParentId = "70727bc1-1744-4ec8-94bf-616fc87600ec",
        RelatedActionPermissionsCsv = CommunicationsPermissions.Configure
    )]
    public const string CommunicationsEmail = "/settings/communications/email";

    // NAV-HIERARCHY-UNIFY-01: contenedor "Sistema" — Parámetros Generales + Carga Inicial.
    [NavItem(
        "Sistema",
        LabelKey = "app.nav.item.settings.systemGroup",
        SortOrder = 80,
        Id = "5ece43ac-3228-445b-9bd8-cf86baef2fa8",
        PermissionsAnyCsv = OperationalPreferencesPermissions.View + "," + InitialLoadPermissions.View
    )]
    public const string SystemGroup = "/settings/system-group";

    // MENU-FINAL-STRUCTURE-01: renombrado de negocio "Parámetros Generales" (antes
    // "Preferencias operativas") — mismo Id/ruta/permiso, misma pantalla.
    [NavItem(
        "Parámetros Generales",
        Permission = OperationalPreferencesPermissions.View,
        LabelKey = "app.nav.item.settings.operationalPreferences",
        SortOrder = 80,
        Id = "a1000000-0000-4000-9000-000000000013",
        ParentId = "5ece43ac-3228-445b-9bd8-cf86baef2fa8",
        RelatedActionPermissionsCsv = OperationalPreferencesPermissions.Configure
    )]
    public const string OperationalPreferences = "/settings/operations";

    [NavItem(
        "Geography",
        Permission = SettingsPermissions.GeographyView,
        LabelKey = "app.nav.item.settings.geography",
        SortOrder = 90,
        Id = "a1000000-0000-4000-9000-000000000006",
        ParentId = "7eabb75d-1ccf-4a4a-a4ee-46a082a7e90d"
    )]
    public const string Geography = "/settings/geography";

    // MAPA-MENU-ERP-SSOT-01: "Condiciones comerciales" (grupo + Condiciones de Pago + Condiciones
    // de Crédito) se movió a SuppliersModule (tile "Proveedores") — el árbol objetivo pide
    // "Condiciones pago/crédito" bajo Proveedores. Mismos Ids
    // (3ac9c729-c29b-4e88-a1eb-b0d8073828c2 / a1000000-0000-4000-9000-000000000103 /
    // b2000000-0000-4000-9000-000000000001), mismas rutas/permisos.

    // DESTINOS-CONTABLES-COBROS-VENTAS-01: NavItem "Formas de cobro" (antes aquí, movido desde
    // SalesModule por PAYMENT-METHOD-ACCOUNT-UI-NAV-01) reubicado a
    // AccountingModule.SalesCollectionDestinations (Contabilidad > Configuración > Destinos
    // contables > Cobros de ventas) — mismo Id (d1000000-0000-4000-9000-000000000002), misma
    // ruta/página/permisos, solo cambia su ubicación en el menú por pedido explícito del ticket.
    // Una sola entrada de menú, no dos.

    // INITIAL-LOAD-ARCH-01: registro de navegación separado del [AppFeature] del controller —
    // el AppFeatureDiscoveryService sincroniza app_features (catálogo de permisos), pero la
    // barra lateral (GET /api/v1/me/menu) se arma desde este KernelRegistry ([Module]/[NavItem]),
    // un mecanismo distinto. Un item nuevo necesita ambos para aparecer en el menú real.
    [NavItem(
        "Carga Inicial",
        Permission = InitialLoadPermissions.View,
        LabelKey = "app.nav.item.settings.initialLoad",
        SortOrder = 100,
        Id = "3679c0d4-3482-42cb-91dc-c3a270aa0e26",
        ParentId = "5ece43ac-3228-445b-9bd8-cf86baef2fa8",
        RelatedActionPermissionsCsv = InitialLoadPermissions.Create + "," + InitialLoadPermissions.Confirm
    )]
    public const string InitialLoad = "/initial-load";
}
