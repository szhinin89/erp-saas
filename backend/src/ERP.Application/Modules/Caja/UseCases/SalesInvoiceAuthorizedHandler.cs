using ERP.Application.Common;
using ERP.Domain.Modules.Caja.Enums;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Caja.UseCases;

public sealed class SalesInvoiceAuthorizedHandler
    : INotificationHandler<SalesInvoiceAuthorizedEvent>
{
    private readonly ICashSessionRepository _cashRepo;
    private readonly ICurrentTenant _t;
    private readonly ILogger<SalesInvoiceAuthorizedHandler> _logger;

    public SalesInvoiceAuthorizedHandler(
        ICashSessionRepository cashRepo,
        ICurrentTenant t,
        ILogger<SalesInvoiceAuthorizedHandler> logger
    )
    {
        _cashRepo = cashRepo;
        _t = t;
        _logger = logger;
    }

    public async Task Handle(SalesInvoiceAuthorizedEvent e, CancellationToken ct)
    {
        // CASH-SESSION-PHYSICAL-CASH-SSOT-01 — Caja registra exclusivamente efectivo físico
        // (e.PhysicalCashApplied: suma de pagos con PaymentMethod.AffectsPhysicalCash = true, hoy
        // solo Efectivo), nunca e.CashApplied (que además incluye Transferencia/Tarjeta/Cheque —
        // dinero real para contabilidad/CxC, pero que nunca entra/sale del cajón físico) ni
        // e.GrandTotal (que incluye la porción a crédito, si la hubiera). Una venta 100% no-efectivo
        // (o a crédito puro) llega aquí con PhysicalCashApplied == 0 — no se crea movimiento de caja
        // en absoluto (ni siquiera de $0): no hubo efectivo físico que registrar, y un movimiento de
        // $0 solo ensuciaría el arqueo.
        if (e.PhysicalCashApplied <= 0)
        {
            _logger.LogInformation(
                "Sales invoice {InvoiceNumber} ({InvoiceId}) authorized with PhysicalCashApplied={PhysicalCashApplied} (no efectivo físico) — no cash movement created.",
                e.InvoiceNumber,
                e.InvoiceId,
                e.PhysicalCashApplied
            );
            return;
        }

        // ZH-SALES-CASH-CONCURRENCY-HARDENING-01 — lock oficial de la sesión (FOR UPDATE + recarga)
        // dentro de la transacción que ya abrió ErpDbContext.SaveChangesAsync, igual que todo flujo
        // que registra movimientos en ella: dos ventas concurrentes sobre la misma sesión se
        // serializan (la segunda ve saldo y movimientos vigentes) en vez de chocar por xmin. Orden de
        // locks: CashSession antes que los locks contables del posting (este handler se registra
        // antes que los traductores de Accounting — ver SalesInvoiceAuthorizedHandlerOrderTests).
        // SalesInvoice.CashSessionId identifica exactamente la sesión que originó la venta.
        var session = await _cashRepo.GetByIdForUpdateAsync(_t.TenantId, e.CashSessionId, ct);
        if (session is null)
        {
            _logger.LogWarning(
                "CashSession {CashSessionId} not found when processing SalesInvoiceAuthorized {InvoiceId}. Movement skipped.",
                e.CashSessionId,
                e.InvoiceId
            );
            return;
        }

        // Idempotencia por origen estable: una factura se autoriza una sola vez, así que tiene como
        // máximo un SaleIncome (ReferenceType SalesInvoice + ReferenceId = la factura). Una
        // re-entrega del mismo evento no crea otro movimiento; ventas distintas nunca coinciden.
        if (session.Movements.Any(m =>
                m.MovementType == CashMovementType.SaleIncome
                && m.ReferenceType == CashReferenceType.SalesInvoice
                && m.ReferenceId == e.InvoiceId))
        {
            _logger.LogInformation(
                "SaleIncome for invoice {InvoiceNumber} ({InvoiceId}) already recorded in session {SessionId} — redelivery ignored.",
                e.InvoiceNumber,
                e.InvoiceId,
                session.Id
            );
            return;
        }

        session.RecordMovement(
            CashMovementType.SaleIncome,
            e.PhysicalCashApplied,
            $"Venta {e.InvoiceNumber}",
            e.UserId,
            CashReferenceType.SalesInvoice,
            e.InvoiceId,
            e.InvoiceNumber
        );

        _logger.LogInformation(
            "Cash movement SaleIncome recorded for invoice {InvoiceNumber} ({InvoiceId}) in session {SessionId}. Amount: {Amount}",
            e.InvoiceNumber,
            e.InvoiceId,
            session.Id,
            e.PhysicalCashApplied
        );
    }
}
