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
        // La caja ya no se busca por usuario (ad-hoc): SalesInvoice.CashSessionId identifica
        // exactamente la sesión que originó la venta (ADR — Rediseño del módulo de Caja, Fase 4).
        var session = await _cashRepo.GetByIdAsync(_t.TenantId, e.CashSessionId, ct);
        if (session is null)
        {
            _logger.LogWarning(
                "CashSession {CashSessionId} not found when processing SalesInvoiceAuthorized {InvoiceId}. Movement skipped.",
                e.CashSessionId,
                e.InvoiceId
            );
            return;
        }

        // SALES-CASH-REAL-MONEY-01 — Caja registra exclusivamente el dinero real recibido
        // (e.CashApplied: suma de pagos con método NO marcado IsCreditAllowed, ya calculada y
        // propagada por AuthorizeSalesInvoiceHandler/SalesInvoice.Authorize), nunca e.GrandTotal
        // (que incluye la porción a crédito, si la hubiera). Una venta a crédito puro llega aquí
        // con CashApplied == 0 — no se crea movimiento de caja en absoluto (ni siquiera de $0):
        // no hubo dinero real que registrar, y un movimiento de $0 solo ensuciaría el arqueo.
        if (e.CashApplied <= 0)
        {
            _logger.LogInformation(
                "Sales invoice {InvoiceNumber} ({InvoiceId}) authorized with CashApplied={CashApplied} (credit sale) — no cash movement created.",
                e.InvoiceNumber,
                e.InvoiceId,
                e.CashApplied
            );
            return;
        }

        session.RecordMovement(
            CashMovementType.SaleIncome,
            e.CashApplied,
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
            e.CashApplied
        );
    }
}
