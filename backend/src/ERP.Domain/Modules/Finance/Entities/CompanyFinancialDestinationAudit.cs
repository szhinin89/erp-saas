using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Finance.Entities;

/// <summary>
/// Auditoría de dominio de <see cref="CompanyFinancialDestination"/> (ADR-022, Entity Audit) —
/// registra antes/después únicamente de los 3 campos editables (<c>Name</c>, <c>IsActive</c>,
/// <c>AccountingAccountId</c>, diseño §20.1, §6.4ter). Append-only — nunca se edita ni se borra.
/// Cada fila puebla únicamente el par Old/New correspondiente al campo realmente modificado por
/// el evento que la origina (Created no puebla ningún Old — no hay estado anterior).
/// </summary>
public sealed class CompanyFinancialDestinationAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public string? Code { get; private set; }
    public string? OldName { get; private set; }
    public string? NewName { get; private set; }
    public bool? OldIsActive { get; private set; }
    public bool? NewIsActive { get; private set; }
    public Guid? OldAccountingAccountId { get; private set; }
    public Guid? NewAccountingAccountId { get; private set; }

    private CompanyFinancialDestinationAudit() { }

    public static CompanyFinancialDestinationAudit Create(
        AuditActor actor,
        Guid companyId,
        Guid financialDestinationId,
        string action,
        string? code = null,
        string? reason = null,
        string? oldName = null,
        string? newName = null,
        bool? oldIsActive = null,
        bool? newIsActive = null,
        Guid? oldAccountingAccountId = null,
        Guid? newAccountingAccountId = null
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new CompanyFinancialDestinationAudit
        {
            CompanyId = companyId,
            Code = code,
            OldName = oldName,
            NewName = newName,
            OldIsActive = oldIsActive,
            NewIsActive = newIsActive,
            OldAccountingAccountId = oldAccountingAccountId,
            NewAccountingAccountId = newAccountingAccountId,
        };
        audit.SetCommon(actor, financialDestinationId, action, reason);
        return audit;
    }
}
