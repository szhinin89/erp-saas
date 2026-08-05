using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Events;
using ERP.Domain.Modules.Purchases.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Finance.EventHandlers;

/// <summary>
/// Traduce los domain events de <see cref="SupplierCredit"/> a <see cref="SupplierCreditAudit"/>
/// (ADR-022, Entity Audit). P0-02 Fase 7 cubrió <c>Applied</c>/<c>ApplicationReversed</c>; P0-02
/// Fase 8 extiende (autorizado explícitamente por el plan §10 Fase 8: "Extensión de
/// SupplierCreditAuditHandler.cs para cubrir Refunded/RefundReversed") con <c>Refunded</c>/
/// <c>RefundReversed</c> — <c>SourceReturnCancelled</c> queda para Fase 10 (Open/Closed, mismo
/// criterio que <c>PurchaseReturnAuditHandler</c>).
///
/// Ninguno de los 4 eventos transporta el grupo "destino financiero" (§20.1: <c>FinancialDestinationId</c>,
/// codes, <c>AccountingAccountId</c>, caja, método, referencia, fecha efectiva) ni
/// <c>BranchId</c>/<c>SupplierId</c> (inmutables del agregado) — este handler los resuelve
/// mediante lecturas de solo lectura de <see cref="ISupplierCreditRepository"/>/
/// <see cref="ISupplierCreditRefundTransactionRepository"/> sobre el mismo <c>ErpDbContext</c>
/// ambiente. Seguro por diseño: el dispatcher FROZEN (<c>ErpDbContext.SaveChangesAsync</c> →
/// Outbox → MediatR) publica los eventos DESPUÉS de que <c>base.SaveChangesAsync()</c> ya escribió
/// la fila en la misma transacción/conexión.
/// </summary>
public sealed class SupplierCreditAuditHandler
    : INotificationHandler<SupplierCreditAppliedEvent>,
        INotificationHandler<SupplierCreditApplicationReversedEvent>,
        INotificationHandler<SupplierCreditRefundedEvent>,
        INotificationHandler<SupplierCreditRefundReversedEvent>
{
    private readonly IAuditService _audit;
    private readonly IAuditContext _context;
    private readonly ISupplierCreditRepository _creditRepo;
    private readonly ISupplierCreditRefundTransactionRepository _txRepo;

    public SupplierCreditAuditHandler(
        IAuditService audit,
        IAuditContext context,
        ISupplierCreditRepository creditRepo,
        ISupplierCreditRefundTransactionRepository txRepo
    )
    {
        _audit = audit;
        _context = context;
        _creditRepo = creditRepo;
        _txRepo = txRepo;
    }

    public async Task Handle(SupplierCreditAppliedEvent e, CancellationToken ct)
    {
        var credit = await _creditRepo.GetByIdAsync(e.TenantId!.Value, e.SupplierCreditId, ct);
        if (credit is null)
            return;

        var movement = credit.Movements.FirstOrDefault(m => m.Id == e.SupplierCreditMovementId);
        var balanceBefore = e.AvailableAmountAfter + e.Amount;

        await _audit.RecordAsync(
            SupplierCreditAudit.Create(
                _context.Actor,
                e.CompanyId,
                credit.BranchId,
                e.SupplierCreditId,
                credit.SupplierId,
                ((IAuditEvent)e).Action,
                movementType: movement?.MovementType ?? SupplierCreditMovementType.Application,
                amount: e.Amount,
                balanceBefore: balanceBefore,
                balanceAfter: e.AvailableAmountAfter,
                statusBefore: balanceBefore > 0 ? "Open" : "Closed",
                statusAfter: e.AvailableAmountAfter > 0 ? "Open" : "Closed",
                targetPurchasePayableId: e.TargetPurchasePayableId
            ),
            ct
        );
    }

    public async Task Handle(SupplierCreditApplicationReversedEvent e, CancellationToken ct)
    {
        var credit = await _creditRepo.GetByIdAsync(e.TenantId!.Value, e.SupplierCreditId, ct);
        if (credit is null)
            return;

        var movement = credit.Movements.FirstOrDefault(m => m.Id == e.SupplierCreditMovementId);
        var balanceBefore = e.AvailableAmountAfter - e.Amount;

        await _audit.RecordAsync(
            SupplierCreditAudit.Create(
                _context.Actor,
                e.CompanyId,
                credit.BranchId,
                e.SupplierCreditId,
                credit.SupplierId,
                ((IAuditEvent)e).Action,
                movementType: movement?.MovementType
                    ?? SupplierCreditMovementType.ReversalOfApplication,
                amount: e.Amount,
                balanceBefore: balanceBefore,
                balanceAfter: e.AvailableAmountAfter,
                statusBefore: balanceBefore > 0 ? "Open" : "Closed",
                statusAfter: e.AvailableAmountAfter > 0 ? "Open" : "Closed",
                targetPurchasePayableId: e.TargetPurchasePayableId
            ),
            ct
        );
    }

    public async Task Handle(SupplierCreditRefundedEvent e, CancellationToken ct)
    {
        var credit = await _creditRepo.GetByIdAsync(e.TenantId!.Value, e.SupplierCreditId, ct);
        if (credit is null)
            return;

        var transaction = await _txRepo.GetBySupplierCreditMovementIdAsync(
            e.TenantId!.Value,
            e.SupplierCreditMovementId,
            ct
        );
        var balanceBefore = e.AvailableAmountAfter + e.Amount;

        await _audit.RecordAsync(
            SupplierCreditAudit.Create(
                _context.Actor,
                e.CompanyId,
                credit.BranchId,
                e.SupplierCreditId,
                credit.SupplierId,
                ((IAuditEvent)e).Action,
                movementType: SupplierCreditMovementType.Refund,
                amount: e.Amount,
                balanceBefore: balanceBefore,
                balanceAfter: e.AvailableAmountAfter,
                statusBefore: balanceBefore > 0 ? "Open" : "Closed",
                statusAfter: e.AvailableAmountAfter > 0 ? "Open" : "Closed",
                financialDestinationId: transaction?.FinancialDestinationId,
                financialDestinationCodeSnapshot: transaction?.FinancialDestinationCodeSnapshot,
                destinationTypeCodeSnapshot: transaction?.DestinationTypeCodeSnapshot,
                accountingAccountId: transaction?.AccountingAccountId,
                cashSessionId: transaction?.CashSessionId,
                cashMovementId: transaction?.CashMovementId,
                paymentMethodCode: transaction?.PaymentMethodCode,
                externalReference: transaction?.ExternalReference,
                effectiveDate: transaction?.EffectiveDate
            ),
            ct
        );
    }

    public async Task Handle(SupplierCreditRefundReversedEvent e, CancellationToken ct)
    {
        var credit = await _creditRepo.GetByIdAsync(e.TenantId!.Value, e.SupplierCreditId, ct);
        if (credit is null)
            return;

        var transaction = await _txRepo.GetBySupplierCreditMovementIdAsync(
            e.TenantId!.Value,
            e.SupplierCreditMovementId,
            ct
        );
        var balanceBefore = e.AvailableAmountAfter - e.Amount;

        await _audit.RecordAsync(
            SupplierCreditAudit.Create(
                _context.Actor,
                e.CompanyId,
                credit.BranchId,
                e.SupplierCreditId,
                credit.SupplierId,
                ((IAuditEvent)e).Action,
                movementType: SupplierCreditMovementType.ReversalOfRefund,
                amount: e.Amount,
                balanceBefore: balanceBefore,
                balanceAfter: e.AvailableAmountAfter,
                statusBefore: balanceBefore > 0 ? "Open" : "Closed",
                statusAfter: e.AvailableAmountAfter > 0 ? "Open" : "Closed",
                financialDestinationId: transaction?.FinancialDestinationId,
                financialDestinationCodeSnapshot: transaction?.FinancialDestinationCodeSnapshot,
                destinationTypeCodeSnapshot: transaction?.DestinationTypeCodeSnapshot,
                accountingAccountId: transaction?.AccountingAccountId,
                cashSessionId: transaction?.CashSessionId,
                cashMovementId: transaction?.CashMovementId,
                paymentMethodCode: transaction?.PaymentMethodCode,
                reason: transaction?.Reason,
                effectiveDate: transaction?.EffectiveDate
            ),
            ct
        );
    }
}
