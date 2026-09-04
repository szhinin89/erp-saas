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

    /// <summary>
    /// DOCUMENT-SEQUENCES-CONFIG-03 — configurar el número inicial de una secuencia documental
    /// SRI (<c>DocumentSequence</c>) antes de su primer uso real. No es un permiso de emisión de
    /// documentos (Facturas/Retenciones ya tienen los suyos) — es administración de numeración,
    /// mismo perfil de riesgo que <see cref="FinancialDestinationsManage"/>.
    /// </summary>
    public const string DocumentSequencesManage = "settings.document-sequences.manage";

    /// <summary>P0-02 §20.2 — administración del catálogo <c>CompanyFinancialDestination</c> (mismo perfil de riesgo que administrar cuentas contables/métodos de pago).</summary>
    public const string FinancialDestinationsView = "settings.financial-destinations.view";
    public const string FinancialDestinationsManage = "settings.financial-destinations.manage";

    /// <summary>
    /// DOCUMENT-FLOW-POLICY-01 — administración de <c>DocumentFlowPolicy</c> (Configuración →
    /// Documentos y flujos). Solo controla el acceso a esta pantalla de configuración — no
    /// reemplaza los permisos de acción de cada módulo (p. ej. <c>expenses.documents.cancel</c>).
    /// </summary>
    public const string DocumentFlowsView = "settings.documentFlows.view";
    public const string DocumentFlowsUpdate = "settings.documentFlows.update";
}
