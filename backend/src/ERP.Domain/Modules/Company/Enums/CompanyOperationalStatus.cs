namespace ERP.Domain.Modules.Company.Enums;

/// <summary>
/// Operational readiness status of a company within the ERP runtime.
/// PendingSetup: histórico (wizard de onboarding eliminado); ya no se asigna a companies nuevas.
/// Operational: empresa lista para operar — estado por defecto al provisionar.
/// Suspended: company suspended by platform operator.
/// </summary>
public enum CompanyOperationalStatus
{
    PendingSetup  = 1,
    Operational   = 2,
    Suspended     = 3,
}
