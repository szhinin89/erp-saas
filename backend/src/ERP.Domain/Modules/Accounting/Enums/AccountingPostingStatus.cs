namespace ERP.Domain.Modules.Accounting.Enums;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — estado de una obligación contable durable
/// (docs/architecture/accounting.md §5).
/// </summary>
public enum AccountingPostingStatus
{
    Pending = 1,
    Failed = 2,
    Posted = 3,
    Canceled = 4,
}
