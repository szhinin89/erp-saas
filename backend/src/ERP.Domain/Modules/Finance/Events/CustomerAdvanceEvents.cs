using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;

namespace ERP.Domain.Modules.Finance.Events;

/// <summary>
/// ADR-052 — datos comunes de un evento de anticipo de cliente: un evento por movimiento (la
/// identidad contable es el movimiento), autocontenido para que la auditoría nunca dependa de una
/// relectura. <c>TenantId</c> viaja en <see cref="BaseDomainEvent"/>.
/// </summary>
public abstract class CustomerAdvanceEventBase : BaseDomainEvent, IAuditEvent
{
    public Guid CustomerAdvanceId { get; }
    public Guid CompanyId { get; }
    public Guid BranchId { get; }
    public Guid CustomerId { get; }
    public Guid MovementId { get; }
    public CustomerAdvanceMovementType MovementType { get; }
    public decimal Amount { get; }
    public decimal AvailableAmountBefore { get; }
    public decimal AvailableAmountAfter { get; }

    /// <summary>Null solo en el ingreso: el anticipo no existía.</summary>
    public CustomerAdvanceStatus? StatusBefore { get; }

    public CustomerAdvanceStatus StatusAfter { get; }
    public Guid ClientRequestId { get; }
    public DateOnly EffectiveDate { get; }
    public DateOnly? EvidenceDate { get; }
    public string? Reason { get; }
    public string? Reference { get; }
    public Guid? ReceivableId { get; }
    public Guid? InstallmentId { get; }
    public Guid? ReversalOfMovementId { get; }

    /// <summary>Medio y destino con que ingresó el anticipo.</summary>
    public Guid PaymentMethodId { get; }

    public Guid? CompanyBankAccountId { get; }
    public Guid? CashRegisterId { get; }
    public Guid UserId { get; }

    Guid IAuditEvent.EntityId => CustomerAdvanceId;
    string IAuditEvent.Action => Action;
    string? IAuditEvent.Reason => Reason;

    /// <summary>Acción de Entity Audit.</summary>
    protected abstract string Action { get; }

    private protected CustomerAdvanceEventBase(
        CustomerAdvance advance,
        CustomerAdvanceMovement movement,
        decimal balanceBefore,
        CustomerAdvanceStatus? statusBefore
    )
    {
        TenantId = advance.TenantId;
        CustomerAdvanceId = advance.Id;
        CompanyId = advance.CompanyId;
        BranchId = movement.BranchId;
        CustomerId = advance.CustomerId;
        MovementId = movement.Id;
        MovementType = movement.MovementType;
        Amount = movement.Amount;
        AvailableAmountBefore = balanceBefore;
        AvailableAmountAfter = advance.AvailableAmount;
        StatusBefore = statusBefore;
        StatusAfter = advance.Status;
        ClientRequestId = movement.ClientRequestId;
        EffectiveDate = movement.EffectiveDate;
        EvidenceDate = movement.EvidenceDate;
        Reason = movement.Reason;
        Reference = movement.Reference;
        ReceivableId = movement.ReceivableId;
        InstallmentId = movement.InstallmentId;
        ReversalOfMovementId = movement.ReversalOfMovementId;
        PaymentMethodId = advance.PaymentMethodId;
        CompanyBankAccountId = advance.CompanyBankAccountId;
        CashRegisterId = advance.CashRegisterId;
        UserId = movement.CreatedByUserId;
    }
}

public sealed class CustomerAdvanceReceivedEvent(
    CustomerAdvance advance,
    CustomerAdvanceMovement movement,
    decimal balanceBefore,
    CustomerAdvanceStatus? statusBefore
) : CustomerAdvanceEventBase(advance, movement, balanceBefore, statusBefore)
{
    protected override string Action => "Received";
}

public sealed class CustomerAdvanceAppliedEvent(
    CustomerAdvance advance,
    CustomerAdvanceMovement movement,
    decimal balanceBefore,
    CustomerAdvanceStatus? statusBefore
) : CustomerAdvanceEventBase(advance, movement, balanceBefore, statusBefore)
{
    protected override string Action => "Applied";
}

public sealed class CustomerAdvanceApplicationReversedEvent(
    CustomerAdvance advance,
    CustomerAdvanceMovement movement,
    decimal balanceBefore,
    CustomerAdvanceStatus? statusBefore
) : CustomerAdvanceEventBase(advance, movement, balanceBefore, statusBefore)
{
    protected override string Action => "ApplicationReversed";
}

public sealed class CustomerAdvanceRefundedEvent(
    CustomerAdvance advance,
    CustomerAdvanceMovement movement,
    decimal balanceBefore,
    CustomerAdvanceStatus? statusBefore
) : CustomerAdvanceEventBase(advance, movement, balanceBefore, statusBefore)
{
    protected override string Action => "Refunded";
}

public sealed class CustomerAdvanceRefundReversedEvent(
    CustomerAdvance advance,
    CustomerAdvanceMovement movement,
    decimal balanceBefore,
    CustomerAdvanceStatus? statusBefore
) : CustomerAdvanceEventBase(advance, movement, balanceBefore, statusBefore)
{
    protected override string Action => "RefundReversed";
}

public sealed class CustomerAdvanceCancelledEvent(
    CustomerAdvance advance,
    CustomerAdvanceMovement movement,
    decimal balanceBefore,
    CustomerAdvanceStatus? statusBefore
) : CustomerAdvanceEventBase(advance, movement, balanceBefore, statusBefore)
{
    protected override string Action => "Cancelled";
}

public sealed class CustomerAdvanceIncomeReversedEvent(
    CustomerAdvance advance,
    CustomerAdvanceMovement movement,
    decimal balanceBefore,
    CustomerAdvanceStatus? statusBefore
) : CustomerAdvanceEventBase(advance, movement, balanceBefore, statusBefore)
{
    protected override string Action => "IncomeReversed";
}
