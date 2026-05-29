namespace ERP.Domain.Modules.Company.Enums;

/// <summary>
/// Operational readiness status of a company within the ERP runtime.
/// PendingSetup: just provisioned, onboarding not yet complete (no Branch/Warehouse/Establishment).
/// Operational: onboarding complete, all ERP infrastructure is in place.
/// Suspended: company suspended by platform operator.
/// </summary>
public enum CompanyOperationalStatus
{
    PendingSetup  = 1,
    Operational   = 2,
    Suspended     = 3,
}
