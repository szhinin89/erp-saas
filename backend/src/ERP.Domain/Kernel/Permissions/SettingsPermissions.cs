namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Permisos del módulo Settings — sucursales, geografía, datos de la empresa
/// y empresas operativas (hub <c>/settings</c> + <c>/companies</c>).
/// </summary>
public static class SettingsPermissions
{
    public const string BranchesView = "settings.branches.view";
    public const string BranchesCreate = "settings.branches.create";
    public const string BranchesUpdate = "settings.branches.update";
    public const string BranchesDelete = "settings.branches.delete";

    public const string EstablishmentsView = "settings.establishments.view";
    public const string EstablishmentsCreate = "settings.establishments.create";
    public const string EstablishmentsUpdate = "settings.establishments.update";
    public const string EstablishmentsDisable = "settings.establishments.disable";

    public const string GeographyView = "settings.geography.view";
    public const string CompanyView = "settings.company.view";
    public const string CompaniesView = "erp.companies.view";
    public const string CompaniesCreate = "erp.companies.create";
    public const string CompaniesUpdate = "erp.companies.update";

    public const string EmissionPointsView = "settings.emission-points.view";
    public const string EmissionPointsCreate = "settings.emission-points.create";
    public const string EmissionPointsUpdate = "settings.emission-points.update";
    public const string EmissionPointsDelete = "settings.emission-points.delete";
}
