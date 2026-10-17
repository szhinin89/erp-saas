using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// ACCOUNTING-07 — Entity Audit (ADR-022) de <see cref="AccountingPeriod"/>: Created/Closed/Locked
/// con el snapshot del ejercicio, número y rango del período. Append-only — nunca se edita ni se borra.
/// </summary>
public sealed class AccountingPeriodAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public int FiscalYear { get; private set; }
    public int PeriodNumber { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }

    private AccountingPeriodAudit() { }

    public static AccountingPeriodAudit Create(
        AuditActor actor,
        Guid companyId,
        Guid accountingPeriodId,
        string action,
        int fiscalYear,
        int periodNumber,
        DateOnly startDate,
        DateOnly endDate
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new AccountingPeriodAudit
        {
            CompanyId = companyId,
            FiscalYear = fiscalYear,
            PeriodNumber = periodNumber,
            StartDate = startDate,
            EndDate = endDate,
        };
        audit.SetCommon(actor, accountingPeriodId, action, reason: null);
        return audit;
    }
}
