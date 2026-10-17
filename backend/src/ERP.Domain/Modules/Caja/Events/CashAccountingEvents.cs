using ERP.Domain.Common;
using ERP.Domain.Modules.Caja.Enums;

namespace ERP.Domain.Modules.Caja.Events;

/// <summary>
/// ACCOUNTING-12C — movimiento manual de caja con motivo (ManualIncome/ManualExpense/Withdrawal)
/// registrado por el cajero. Es el hecho económico: Accounting decide si genera obligación. Solo lo
/// publica <c>CashSession.RecordManualMovement</c>; los movimientos de sistema (y los ManualIncome/
/// ManualExpense internos de Finance) los contabiliza su documento de origen, nunca este evento.
/// </summary>
public sealed class CashManualMovementRecordedEvent : BaseDomainEvent
{
    public Guid CompanyId { get; }
    public Guid CashSessionId { get; }
    public Guid CashRegisterId { get; }
    public Guid CashMovementId { get; }
    public CashMovementType MovementType { get; }
    public decimal Amount { get; }
    public Guid ReasonId { get; }
    public string Description { get; }
    public DateTime RecordedAtUtc { get; }

    public CashManualMovementRecordedEvent(Guid tenantId, Guid companyId, Guid cashSessionId,
        Guid cashRegisterId, Guid cashMovementId, CashMovementType movementType, decimal amount,
        Guid reasonId, string description, DateTime recordedAtUtc)
    {
        TenantId = tenantId; CompanyId = companyId; CashSessionId = cashSessionId;
        CashRegisterId = cashRegisterId; CashMovementId = cashMovementId; MovementType = movementType;
        Amount = amount; ReasonId = reasonId; Description = description; RecordedAtUtc = recordedAtUtc;
    }
}

/// <summary>ACCOUNTING-12C — depósito del efectivo de la caja en una cuenta bancaria de la empresa.</summary>
public sealed class CashBankDepositRecordedEvent : BaseDomainEvent
{
    public Guid CompanyId { get; }
    public Guid CashSessionId { get; }
    public Guid CashRegisterId { get; }
    public Guid CashMovementId { get; }
    public Guid CompanyBankAccountId { get; }
    public decimal Amount { get; }
    public string? ReferenceNumber { get; }
    public DateTime RecordedAtUtc { get; }

    public CashBankDepositRecordedEvent(Guid tenantId, Guid companyId, Guid cashSessionId,
        Guid cashRegisterId, Guid cashMovementId, Guid companyBankAccountId, decimal amount,
        string? referenceNumber, DateTime recordedAtUtc)
    {
        TenantId = tenantId; CompanyId = companyId; CashSessionId = cashSessionId;
        CashRegisterId = cashRegisterId; CashMovementId = cashMovementId;
        CompanyBankAccountId = companyBankAccountId; Amount = amount; ReferenceNumber = referenceNumber;
        RecordedAtUtc = recordedAtUtc;
    }
}

/// <summary>
/// ACCOUNTING-12C — la sesión se cerró con diferencia de arqueo distinta de cero
/// (<see cref="Difference"/> &gt; 0 sobrante, &lt; 0 faltante). Sin diferencia no se publica.
/// </summary>
public sealed class CashSessionClosedWithDifferenceEvent : BaseDomainEvent
{
    public Guid CompanyId { get; }
    public Guid CashSessionId { get; }
    public Guid CashRegisterId { get; }
    public decimal Difference { get; }
    public DateTime ClosedAtUtc { get; }

    public CashSessionClosedWithDifferenceEvent(Guid tenantId, Guid companyId, Guid cashSessionId,
        Guid cashRegisterId, decimal difference, DateTime closedAtUtc)
    {
        TenantId = tenantId; CompanyId = companyId; CashSessionId = cashSessionId;
        CashRegisterId = cashRegisterId; Difference = difference; ClosedAtUtc = closedAtUtc;
    }
}
