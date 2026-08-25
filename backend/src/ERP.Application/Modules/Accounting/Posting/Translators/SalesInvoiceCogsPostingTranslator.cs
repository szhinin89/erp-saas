using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Accounting.Posting.Translators;

/// <summary>
/// ACCOUNTING-INVENTORY-COGS-07 — segundo handler de <see cref="SalesInvoiceAuthorizedEvent"/>
/// (junto a <see cref="SalesInvoiceAuthorizedPostingTranslator"/>, que solo cubre el lado ingreso/
/// IVA) — no se inventó ningún evento nuevo: MediatR ya despacha un mismo evento a múltiples
/// <c>INotificationHandler</c> (mismo patrón que <c>PurchaseInvoiceAuditHandler</c> +
/// <c>PurchaseInvoiceConfirmedPostingTranslator</c> sobre <c>PurchaseInvoiceConfirmedEvent</c>).
/// </summary>
/// <remarks>
/// <see cref="SalesInvoiceAuthorizedEvent"/> no carga ningún monto de costo — el costo real de la
/// salida de inventario (Kardex/costo promedio móvil) se resuelve consultando
/// <see cref="IStockRepository.GetMovementsByDocumentAsync"/> por (InvoiceId, "SalesInvoice"),
/// exactamente los <c>StockMovement</c> que <c>AuthorizeSalesInvoiceHandler</c> ya creó en la misma
/// transacción de la venta — nunca se recalcula desde <c>UnitPrice</c>/<c>GrandTotal</c>. Si la
/// venta no tiene líneas con inventario (solo servicios) o el costo total resuelto es cero, no se
/// publica ningún <c>PostingFact</c> — un asiento de costo en cero no aporta valor contable y
/// nunca debe generarse (mismo criterio que "líneas en cero se omiten" de <c>JournalFactory</c>).
/// FactType "CostOfGoodsSold" es un hecho contable DISTINTO de "InvoiceIssued" para el mismo
/// SourceEventId (InvoiceId) — la clave de idempotencia
/// (CompanyId, SourceModule, SourceEventId, FactType) los mantiene separados, cada uno con su
/// propio JournalEntry independiente. Log-and-continue: un fallo al contabilizar el costo nunca
/// revierte la venta ya autorizada.
/// </remarks>
public sealed class SalesInvoiceCogsPostingTranslator : INotificationHandler<SalesInvoiceAuthorizedEvent>
{
    private const string SourceModuleName = "Sales";
    private const string FactTypeName = "CostOfGoodsSold";
    private const string StockDocumentType = "SalesInvoice";

    private readonly IStockRepository _stockRepository;
    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<SalesInvoiceCogsPostingTranslator> _logger;

    public SalesInvoiceCogsPostingTranslator(
        IStockRepository stockRepository,
        IPostingEngine postingEngine,
        ILogger<SalesInvoiceCogsPostingTranslator> logger
    )
    {
        _stockRepository = stockRepository;
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task Handle(SalesInvoiceAuthorizedEvent e, CancellationToken ct)
    {
        var movements = await _stockRepository.GetMovementsByDocumentAsync(
            e.TenantId!.Value,
            e.InvoiceId,
            StockDocumentType,
            ct
        );

        var totalCost = movements.Sum(m => m.TotalCost ?? 0m);
        if (totalCost <= 0m)
        {
            _logger.LogInformation(
                "Sin costo de inventario que contabilizar para SalesInvoice {InvoiceId} ({InvoiceNumber}) — "
                    + "venta sin líneas de inventario o costo resuelto en cero.",
                e.InvoiceId,
                e.InvoiceNumber
            );
            return;
        }

        var fact = new PostingFact(
            e.TenantId!.Value,
            e.CompanyId,
            SourceModuleName,
            FactTypeName,
            e.InvoiceId,
            e.IssueDate,
            Subtotal: 0m,
            TotalVat: 0m,
            TotalIce: 0m,
            TotalDiscount: 0m,
            GrandTotal: 0m,
            HistoricalCostTotal: totalCost
        );

        var result = await _postingEngine.PostAsync(fact, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Posting de costo de ventas falló para SalesInvoice {InvoiceId} ({InvoiceNumber}): {Code} — {Error}",
                e.InvoiceId,
                e.InvoiceNumber,
                result.Code,
                result.Error
            );
        }
    }
}
