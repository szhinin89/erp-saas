namespace ERP.Domain.Modules.Accounting.Enums;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — uso operativo de Accounting por Company, separado de la obligación
/// fiscal (<c>Company.IsAccountingReq</c>). Ver docs/architecture/accounting.md §3.
/// </summary>
public enum AccountingOperationalMode
{
    Enabled = 1,
    Disabled = 2,
}
