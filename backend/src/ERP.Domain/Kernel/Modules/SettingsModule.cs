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
    // MENU-FINAL-STRUCTURE-01: "Mis empresas" (multiempresa del suscriptor, perm.
    // CompaniesView) y "Datos de la empresa" (empresa activa, perm. CompanyView) son pantallas
    // reales distintas con permisos distintos — no se pueden fusionar en una sola entrada sin
    // perder funcionalidad o mezclar permisos. Se agrupan bajo un contenedor "Empresas" (mismo
    // patrón ya usado en Ventas/Compras/Inventario/Caja) para que el nivel superior de
    // Configuración muestre una sola línea "Empresas", sin borrar ninguna pantalla.
    [NavItem(
        "Empresas",
        LabelKey = "app.nav.item.settings.companiesGroup",
        SortOrder = 5,
        Id = "00000000-0000-4000-8000-000000000105",
        PermissionsAnyCsv = SettingsPermissions.CompaniesView + "," + SettingsPermissions.CompanyView
    )]
    public const string CompaniesGroup = "/settings/companies-group";

    [NavItem(
        "Mis empresas",
        Permission = SettingsPermissions.CompaniesView,
        LabelKey = "app.nav.item.erp.companies",
        SortOrder = 10,
        Id = "00000000-0000-4000-8000-000000000104",
        ParentId = "00000000-0000-4000-8000-000000000105"
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
        ParentId = "00000000-0000-4000-8000-000000000105"
    )]
    public const string Company = "/settings/company";

    [NavItem(
        "Branches",
        Permission = SettingsPermissions.BranchesView,
        LabelKey = "app.nav.item.settings.branches",
        SortOrder = 20,
        Id = "a1000000-0000-4000-9000-000000000005"
    )]
    public const string Branches = "/settings/branches";

    [NavItem(
        "Establishments",
        Permission = SettingsPermissions.EstablishmentsView,
        LabelKey = "app.nav.item.settings.establishments",
        SortOrder = 30,
        Id = "a1000000-0000-4000-9000-000000000010"
    )]
    public const string Establishments = "/settings/establishments";

    [NavItem(
        "Emission Points",
        Permission = SettingsPermissions.EmissionPointsView,
        LabelKey = "app.nav.item.settings.emissionPoints",
        SortOrder = 40,
        Id = "a1000000-0000-4000-9000-00000000000f"
    )]
    public const string EmissionPoints = "/settings/emission-points";

    [NavItem(
        "Destinos financieros",
        Permission = SettingsPermissions.FinancialDestinationsView,
        LabelKey = "app.nav.item.settings.financialDestinations",
        SortOrder = 50,
        Id = "a1000000-0000-4000-9000-000000000011"
    )]
    public const string FinancialDestinations = "/settings/financial-destinations";

    // MENU-MODULE-REORG-01: movido desde SalesModule — es configuración transversal (aplica al
    // documento electrónico en general), no exclusiva de Ventas. Mismo permiso; antes derivaba
    // su Id automáticamente (module.Code="sales" + ruta), ahora fijo explícito para que el
    // cambio de módulo no genere un Id nuevo huérfano en ui_nav_items.
    [NavItem(
        "Electronic Invoicing",
        Permission = ElectronicInvoicingPermissions.View,
        LabelKey = "app.nav.item.settings.electronicInvoicing",
        SortOrder = 60,
        Id = "a1000000-0000-4000-9000-000000000014"
    )]
    public const string ElectronicInvoicing = "/settings/electronic-invoicing";

    [NavItem(
        "Correo SMTP",
        Permission = CommunicationsPermissions.View,
        LabelKey = "app.nav.item.settings.communicationsEmail",
        SortOrder = 70,
        Id = "a1000000-0000-4000-9000-000000000012"
    )]
    public const string CommunicationsEmail = "/settings/communications/email";

    // MENU-FINAL-STRUCTURE-01: renombrado de negocio "Parámetros Generales" (antes
    // "Preferencias operativas") — mismo Id/ruta/permiso, misma pantalla.
    [NavItem(
        "Parámetros Generales",
        Permission = OperationalPreferencesPermissions.View,
        LabelKey = "app.nav.item.settings.operationalPreferences",
        SortOrder = 80,
        Id = "a1000000-0000-4000-9000-000000000013"
    )]
    public const string OperationalPreferences = "/settings/operations";

    [NavItem(
        "Geography",
        Permission = SettingsPermissions.GeographyView,
        LabelKey = "app.nav.item.settings.geography",
        SortOrder = 90,
        Id = "a1000000-0000-4000-9000-000000000006"
    )]
    public const string Geography = "/settings/geography";
}
