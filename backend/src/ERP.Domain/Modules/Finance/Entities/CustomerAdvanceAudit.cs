using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;

namespace ERP.Domain.Modules.Finance.Entities;

/// <summary>
/// ADR-052 §11 — Entity Audit (ADR-022) de <see cref="CustomerAdvance"/>: una fila append-only por
/// movimiento. Actor, instante, acción y motivo viven en <see cref="AuditRecordBase"/>.
/// </summary>
public sealed class CustomerAdvanceAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public const int StatusMaxLen = 20;

    public Guid CompanyId { get; private set; }

    /// <summary>Sucursal de la operación auditada.</summary>
    public Guid BranchId { get; private set; }

    public Guid CustomerId { get; private set; }
    public Guid MovementId { get; private set; }
    public CustomerAdvanceMovementType MovementType { get; private set; }
    public decimal Amount { get; private set; }
    public decimal BalanceBefore { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public string? StatusBefore { get; private set; }
    public string StatusAfter { get; private set; } = null!;
    public Guid ClientRequestId { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public DateOnly? EvidenceDate { get; private set; }
    public string? Reference { get; private set; }
    public Guid? ReceivableId { get; private set; }
    public Guid? InstallmentId { get; private set; }
    public Guid? ReversalOfMovementId { get; private set; }
    public Guid PaymentMethodId { get; private set; }
    public Guid? CompanyBankAccountId { get; private set; }
    public Guid? CashRegisterId { get; private set; }
    public Guid? CashSessionId { get; private set; }
    public Guid? CashMovementId { get; private set; }

    private CustomerAdvanceAudit() { }

    /// <summary>
    /// Registra la operación descrita por el evento. <paramref name="refund"/> solo en devolución y
    /// su reversa: su medio y destino sustituyen a los del ingreso.
    /// </summary>
    public static CustomerAdvanceAudit Create(
        AuditActor actor,
        CustomerAdvanceEventBase @event,
        CustomerAdvanceRefundTransaction? refund = null,
        Guid? cashSessionId = null,
        Guid? cashMovementId = null
    )
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (@event.TenantId != actor.TenantId)
            throw new ArgumentException(
                "El actor no pertenece al tenant de la operación.",
                nameof(actor)
            );

        var isRefund =
            @event.MovementType
            is CustomerAdvanceMovementType.Refund
                or CustomerAdvanceMovementType.ReversalOfRefund;
        var audit = new CustomerAdvanceAudit
        {
            CompanyId = @event.CompanyId,
            BranchId = @event.BranchId,
            CustomerId = @event.CustomerId,
            MovementId = @event.MovementId,
            MovementType = @event.MovementType,
            Amount = @event.Amount,
            BalanceBefore = @event.AvailableAmountBefore,
            BalanceAfter = @event.AvailableAmountAfter,
            StatusBefore = @event.StatusBefore?.ToString(),
            StatusAfter = @event.StatusAfter.ToString(),
            ClientRequestId = @event.ClientRequestId,
            EffectiveDate = @event.EffectiveDate,
            EvidenceDate = @event.EvidenceDate,
            Reference = @event.Reference,
            ReceivableId = @event.ReceivableId,
            InstallmentId = @event.InstallmentId,
            ReversalOfMovementId = @event.ReversalOfMovementId,
            // En una devolución sin transacción localizable no se atribuye el destino del ingreso.
            PaymentMethodId = refund?.PaymentMethodId ?? @event.PaymentMethodId,
            CompanyBankAccountId = isRefund
                ? refund?.CompanyBankAccountId
                : @event.CompanyBankAccountId,
            CashRegisterId = isRefund ? refund?.CashRegisterId : @event.CashRegisterId,
            CashSessionId = refund?.CashSessionId ?? cashSessionId,
            CashMovementId = refund?.CashMovementId ?? cashMovementId,
        };
        var auditEvent = (IAuditEvent)@event;
        audit.SetCommon(actor, @event.CustomerAdvanceId, auditEvent.Action, auditEvent.Reason);
        return audit;
    }
}
