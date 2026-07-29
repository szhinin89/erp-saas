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

        session.RecordMovement(
            CashMovementType.SaleIncome,
            e.GrandTotal,
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
            e.GrandTotal
        );
    }
}
