namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Estructura plana (Create/Update/Delete compartidos entre Account/AccountingPeriod/
/// PostingRule) — mismo patrón que <see cref="PricingPermissions"/>, sin granularidad por
/// sub-recurso. Delete se usa para baja lógica (Disable*), nunca para DELETE físico.
/// </summary>
public static class AccountingPermissions
{
    public const string View   = "accounting.view";
    public const string Create = "accounting.create";
    public const string Update = "accounting.update";
    public const string Delete = "accounting.delete";
}
