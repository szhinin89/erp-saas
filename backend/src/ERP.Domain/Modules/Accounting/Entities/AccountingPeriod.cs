using ERP.Domain.Common;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Events;
using ERP.Domain.Modules.Accounting.ValueObjects;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// Período contable. CompanyId-scoped obligatorio (ADR-026 §2). El cierre es comportamiento
/// de dominio (<see cref="Close"/>/<see cref="Lock"/>) — nunca una actualización directa de
/// <see cref="Status"/> desde Application/Infrastructure (ADR-026 §6.1).
/// </summary>
/// <remarks>
/// El invariante "no existen períodos solapados dentro de una Company" (ADR-026 §6.1) es
/// cross-aggregate — no puede validarse dentro de una única instancia de
/// <see cref="AccountingPeriod"/>. Queda pendiente para la capa de Application/Repository
/// cuando se implemente persistencia (fase posterior), no es responsabilidad de este esqueleto.
/// </remarks>
public sealed class AccountingPeriod
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public int FiscalYear { get; private set; }
    public int PeriodNumber { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public PeriodStatus Status { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public Guid? ClosedBy { get; private set; }

    private AccountingPeriod() { }

    public static AccountingPeriod Create(
        Guid tenantId,
        Guid companyId,
        int fiscalYear,
        int periodNumber,
        DateOnly startDate,
        DateOnly endDate,
        Guid createdBy
    )
    {
        if (endDate <= startDate)
            throw new ArgumentException(
                "La fecha de fin debe ser posterior a la fecha de inicio.",
                nameof(endDate)
            );

        var period = new AccountingPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            FiscalYear = fiscalYear,
            PeriodNumber = periodNumber,
            StartDate = startDate,
            EndDate = endDate,
            Status = PeriodStatus.Open,
        };
        period.SetCreated(createdBy);
        period.RaiseDomainEvent(new AccountingPeriodCreatedEvent(tenantId, period.Id, companyId));
        return period;
    }

    /// <summary>Invariante ADR-026 §6.1: un JournalEntry no puede ejecutar Post() contra un período Closed o Locked.</summary>
    public void EnsureOpenForPosting()
    {
        if (Status != PeriodStatus.Open)
            throw new InvalidOperationException(
                $"El período {FiscalYear}-{PeriodNumber} no admite contabilización en estado {Status}."
            );
    }

    /// <summary>
    /// Cierre real del período (Fase 5.5, ADR-026 §6.1/§9). <paramref name="readiness"/> es
    /// cross-aggregate — resuelto por <c>IJournalEntryRepository.GetClosureReadinessAsync</c> en
    /// Application/Infrastructure antes de invocar este método (el aggregate no conoce
    /// <c>JournalEntry</c> directamente, mismo criterio que el invariante "sin períodos
    /// solapados"). Un cierre rechazado no muta ningún campo del período.
    /// </summary>
    public void Close(Guid closedBy, JournalEntryClosureReadiness readiness)
    {
        EnsureOpenForPosting();

        if (!readiness.IsReady)
        {
            var reasons = string.Join("; ", readiness.BuildBlockingReasons());
            throw new InvalidOperationException(
                $"El período {FiscalYear}-{PeriodNumber} no puede cerrarse: {reasons}."
            );
        }

        Status = PeriodStatus.Closed;
        ClosedAtUtc = DateTime.UtcNow;
        ClosedBy = closedBy;
        SetUpdated(closedBy);
        RaiseDomainEvent(new AccountingPeriodClosedEvent(TenantId, Id, CompanyId));
    }

    public void Lock(Guid lockedBy)
    {
        if (Status != PeriodStatus.Closed)
            throw new InvalidOperationException(
                "Solo un período en estado Closed puede pasar a Locked."
            );
        Status = PeriodStatus.Locked;
        SetUpdated(lockedBy);
        RaiseDomainEvent(new AccountingPeriodLockedEvent(TenantId, Id, CompanyId));
    }
}
