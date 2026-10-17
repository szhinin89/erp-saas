using ERP.Domain.Common;
using ERP.Domain.Exceptions;
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
/// Los invariantes cross-aggregate (sin solape, contigüidad, orden frente a períodos cerrados,
/// numeración dentro del ejercicio) viven en <c>AccountingPeriodStructure</c> (Application); el
/// solape tiene además la exclusión PostgreSQL <c>ex_accounting_periods_company_no_overlap</c>
/// como defensa final (ACCOUNTING-07, docs/architecture/accounting.md §8). Aquí solo se valida lo
/// que una única instancia puede garantizar: rango, ejercicio = año calendario de ambas fechas y
/// número 1..<see cref="MaxPeriodsPerFiscalYear"/>.
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
    public DateTime? LockedAtUtc { get; private set; }
    public Guid? LockedBy { get; private set; }

    /// <summary>ACCOUNTING-07: el ejercicio es el año calendario y admite como máximo 12 períodos (mensual).</summary>
    public const int MaxPeriodsPerFiscalYear = 12;

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
        if (endDate < startDate)
            throw new ArgumentException(
                "La fecha de fin no puede ser anterior a la fecha de inicio.",
                nameof(endDate)
            );
        if (startDate.Year != fiscalYear || endDate.Year != fiscalYear)
            throw new ArgumentException(
                $"Las fechas del período deben pertenecer al ejercicio {fiscalYear}.",
                nameof(fiscalYear)
            );
        if (periodNumber is < 1 or > MaxPeriodsPerFiscalYear)
            throw new ArgumentException(
                $"El número de período debe estar entre 1 y {MaxPeriodsPerFiscalYear}.",
                nameof(periodNumber)
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
        period.RaiseDomainEvent(new AccountingPeriodCreatedEvent(period));
        return period;
    }

    /// <summary>Invariante ADR-026 §6.1: un JournalEntry no puede ejecutar Post() contra un período Closed o Locked.</summary>
    public void EnsureOpenForPosting()
    {
        if (Status != PeriodStatus.Open)
            throw new DomainRuleViolationException(
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
        if (Status != PeriodStatus.Open)
            throw new DomainRuleViolationException(
                $"El período {FiscalYear}-{PeriodNumber} ya no está abierto (estado {Status}): no puede cerrarse de nuevo."
            );

        if (!readiness.IsReady)
        {
            var reasons = string.Join("; ", readiness.BuildBlockingReasons());
            throw new DomainRuleViolationException(
                $"El período {FiscalYear}-{PeriodNumber} no puede cerrarse: {reasons}."
            );
        }

        Status = PeriodStatus.Closed;
        ClosedAtUtc = DateTime.UtcNow;
        ClosedBy = closedBy;
        SetUpdated(closedBy);
        RaiseDomainEvent(new AccountingPeriodClosedEvent(this));
    }

    /// <summary>
    /// ACCOUNTING-07: marca final administrativa/auditiva, irreversible. No cambia lo que el período
    /// admite (Closed ya rechaza toda escritura); no existe reapertura ni desbloqueo.
    /// </summary>
    public void Lock(Guid lockedBy)
    {
        if (Status != PeriodStatus.Closed)
            throw new DomainRuleViolationException(
                Status == PeriodStatus.Locked
                    ? $"El período {FiscalYear}-{PeriodNumber} ya está bloqueado."
                    : "Solo un período en estado Closed puede pasar a Locked."
            );
        Status = PeriodStatus.Locked;
        LockedAtUtc = DateTime.UtcNow;
        LockedBy = lockedBy;
        SetUpdated(lockedBy);
        RaiseDomainEvent(new AccountingPeriodLockedEvent(this));
    }
}
