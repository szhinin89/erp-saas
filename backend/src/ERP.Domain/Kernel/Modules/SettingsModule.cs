using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("settings", Icon = "⚙", SortOrder = 50, GroupId = "f2d0ca10-0000-4000-8000-000000000008")]
public static class SettingsModule
{
    /// <summary>
    /// ADMIN-COMPANIES-REGROUP-01: movida desde AdminModule (grupo Administración) — administra
    /// datos de empresa/fiscales/branding/documentos/operación de cada Company del tenant, lo que
    /// conceptualmente es Configuración, no usuarios/perfiles/delegación/sesiones/actividad. Mismo
    /// Id/ruta/permiso que tenía en AdminModule — sin cambios de API ni de lógica de negocio.
    /// </summary>
    // NAV-HIERARCHY-UNIFY-01: contenedor "Empresa" — agrupa todos los catálogos de identidad/
    // ubicación de empresa (Mis empresas, datos de empresa, Sucursales, Establecimientos, Puntos de emisión, Destinos
    // financieros, Geografía) para que ninguno quede suelto bajo el módulo Configuración.
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
            + "," + SettingsPermissions.EmissionPointsView + ","
            + SettingsPermissions.FinancialDestinationsView + "," + SettingsPermissions.GeographyView
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

    [NavItem(
        "Destinos financieros",
        Permission = SettingsPermissions.FinancialDestinationsView,
        LabelKey = "app.nav.item.settings.financialDestinations",
        SortOrder = 50,
        Id = "a1000000-0000-4000-9000-000000000011",
        ParentId = "7eabb75d-1ccf-4a4a-a4ee-46a082a7e90d",
        RelatedActionPermissionsCsv = SettingsPermissions.FinancialDestinationsManage
    )]
    public const string FinancialDestinations = "/settings/financial-destinations";

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

    // NAV-HIERARCHY-UNIFY-01: contenedor "Facturación electrónica" — categoría propia.
    [NavItem(
        "Facturación electrónica",
        LabelKey = "app.nav.item.settings.electronicInvoicingGroup",
        SortOrder = 60,
        Id = "60530be0-ce1c-4a1c-b1e8-fa5b4256bde7",
        PermissionsAnyCsv = ElectronicInvoicingPermissions.View
    )]
    public const string ElectronicInvoicingGroup = "/settings/electronic-invoicing/group";

    // MENU-MODULE-REORG-01: movido desde SalesModule — es configuración transversal (aplica al
    // documento electrónico en general), no exclusiva de Ventas. Mismo permiso; antes derivaba
    // su Id automáticamente (module.Code="sales" + ruta), ahora fijo explícito para que el
    // cambio de módulo no genere un Id nuevo huérfano en ui_nav_items.
    [NavItem(
        "Electronic Invoicing",
        Permission = ElectronicInvoicingPermissions.View,
        LabelKey = "app.nav.item.settings.electronicInvoicing",
        SortOrder = 60,
        Id = "a1000000-0000-4000-9000-000000000014",
        ParentId = "60530be0-ce1c-4a1c-b1e8-fa5b4256bde7",
        RelatedActionPermissionsCsv = ElectronicInvoicingPermissions.Configure
    )]
    public const string ElectronicInvoicing = "/settings/electronic-invoicing";

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

    // NAV-HIERARCHY-UNIFY-01: contenedor "Condiciones comerciales" — Condiciones de Pago +
    // Condiciones de Crédito, catálogos transversales (no exclusivos de Clientes ni de
    // Proveedores) que antes quedaban sueltos bajo el módulo Configuración.
    [NavItem(
        "Condiciones comerciales",
        LabelKey = "app.nav.item.settings.commercialTermsGroup",
        SortOrder = 92,
        Id = "3ac9c729-c29b-4e88-a1eb-b0d8073828c2",
        PermissionsAnyCsv = MasterDataPermissions.PaymentTermsView + "," + FinancePermissions.View
    )]
    public const string CommercialTermsGroup = "/master/commercial-terms-group";

    // NAVIGATION-OPERATING-CYCLES-03: movidos desde MasterDataModule — son catálogos/parámetros
    // transversales (no exclusivos de Clientes ni de Proveedores). Mismos Ids/rutas/permisos.
    [NavItem(
        "Condiciones de Pago",
        Permission = MasterDataPermissions.PaymentTermsView,
        LabelKey = "app.nav.item.masterdata.paymentTerms",
        SortOrder = 92,
        Id = "a1000000-0000-4000-9000-000000000103",
        ParentId = "3ac9c729-c29b-4e88-a1eb-b0d8073828c2",
        RelatedActionPermissionsCsv = MasterDataPermissions.PaymentTermsManage
    )]
    public const string PaymentTermsCustomer = "/master/payment-terms";

    // ADMIN-PERMISSIONS-ACTION-SCOPE-AUDIT-03: Create/Update (CreditTermsPage.tsx →
    // creditTermService.create/update/enable/disable, CreditTermsController) son acciones reales
    // de esta pantalla y no estaban en el catálogo asignable.
    [NavItem(
        "Condiciones de Crédito",
        Permission = FinancePermissions.View,
        LabelKey = "app.nav.item.finance.creditTerms",
        SortOrder = 94,
        Id = "b2000000-0000-4000-9000-000000000001",
        ParentId = "3ac9c729-c29b-4e88-a1eb-b0d8073828c2",
        RelatedActionPermissionsCsv = FinancePermissions.Create + "," + FinancePermissions.Update
    )]
    public const string CreditTerms = "/finance/credit-terms";

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
