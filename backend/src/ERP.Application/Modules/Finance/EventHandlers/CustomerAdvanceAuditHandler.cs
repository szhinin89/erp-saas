using ERP.Application.Audit;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Finance.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Finance.EventHandlers;

/// <summary>
/// ADR-052 §11 — traduce los eventos de <see cref="CustomerAdvance"/> a
/// <see cref="CustomerAdvanceAudit"/> (ADR-022, Entity Audit), dentro de la transacción de la
/// operación. Siempre registra: las lecturas solo completan destino y caja.
/// </summary>
public sealed class CustomerAdvanceAuditHandler(
    IAuditService audit,
    IAuditContext context,
    ICustomerAdvanceRepository advances,
    ICustomerAdvanceRefundTransactionRepository refunds
)
    : INotificationHandler<CustomerAdvanceReceivedEvent>,
        INotificationHandler<CustomerAdvanceAppliedEvent>,
        INotificationHandler<CustomerAdvanceApplicationReversedEvent>,
        INotificationHandler<CustomerAdvanceRefundedEvent>,
        INotificationHandler<CustomerAdvanceRefundReversedEvent>,
        INotificationHandler<CustomerAdvanceCancelledEvent>,
        INotificationHandler<CustomerAdvanceIncomeReversedEvent>
{
    public Task Handle(CustomerAdvanceReceivedEvent e, CancellationToken ct) => RecordAsync(e, ct);

    public Task Handle(CustomerAdvanceAppliedEvent e, CancellationToken ct) => RecordAsync(e, ct);

    public Task Handle(CustomerAdvanceApplicationReversedEvent e, CancellationToken ct) =>
        RecordAsync(e, ct);

    public Task Handle(CustomerAdvanceRefundedEvent e, CancellationToken ct) => RecordAsync(e, ct);

    public Task Handle(CustomerAdvanceRefundReversedEvent e, CancellationToken ct) =>
        RecordAsync(e, ct);

    public Task Handle(CustomerAdvanceCancelledEvent e, CancellationToken ct) => RecordAsync(e, ct);

    public Task Handle(CustomerAdvanceIncomeReversedEvent e, CancellationToken ct) =>
        RecordAsync(e, ct);

    private async Task RecordAsync(CustomerAdvanceEventBase e, CancellationToken ct)
    {
        CustomerAdvanceRefundTransaction? refund = null;
        CustomerAdvanceMovement? movement = null;

        if (
            e.MovementType
            is CustomerAdvanceMovementType.Refund
                or CustomerAdvanceMovementType.ReversalOfRefund
        )
        {
            refund = await refunds.GetByMovementIdAsync(e.TenantId!.Value, e.MovementId, ct);
        }
        else if (
            e.CashRegisterId is not null
            && e.MovementType
                is CustomerAdvanceMovementType.Received
                    or CustomerAdvanceMovementType.IncomeReversed
        )
        {
            var advance = await advances.GetByIdAsync(e.TenantId!.Value, e.CustomerAdvanceId, ct);
            movement = advance?.Movements.FirstOrDefault(m => m.Id == e.MovementId);
        }

        await audit.RecordAsync(
            CustomerAdvanceAudit.Create(
                context.Actor,
                e,
                refund,
                movement?.CashSessionId,
                movement?.CashMovementId
            ),
            ct
        );
    }
}
