using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Purchases.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

/// <summary>
/// FLOW-READY-02D.1 — resumen fiscal persistido de una <c>PurchaseInvoice</c> confirmada, generado
/// exclusivamente por <c>PurchaseInvoice.Confirm()</c> desde las líneas ya congeladas. Solo lectura:
/// no expone ningún comando de creación/edición manual, mismo patrón que
/// <see cref="GetReturnableLinesByPurchaseInvoiceQuery"/>. FLOW-READY-02C-R1.2 agrega
/// <c>CreditedTaxableBase</c>/<c>AvailableTaxableBase</c> por grupo, consultando
/// <c>IPurchaseCreditNoteRepository</c> — única forma en que este query cruza hacia el módulo de
/// notas de crédito, y solo de lectura (nunca escribe ni conecta el flujo de creación aquí).
/// </summary>
public sealed record GetPurchaseInvoiceTaxSummariesQuery(Guid PurchaseInvoiceId)
    : IRequest<Result<IReadOnlyList<PurchaseInvoiceTaxSummaryDto>>>,
        IBranchScopedRequest;

public sealed class GetPurchaseInvoiceTaxSummariesHandler
    : IRequestHandler<
        GetPurchaseInvoiceTaxSummariesQuery,
        Result<IReadOnlyList<PurchaseInvoiceTaxSummaryDto>>
    >
{
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IPurchaseCreditNoteRepository _creditNoteRepo;
    private readonly ICurrentTenant _t;

    public GetPurchaseInvoiceTaxSummariesHandler(
        IPurchaseInvoiceRepository invoiceRepo,
        IPurchaseCreditNoteRepository creditNoteRepo,
        ICurrentTenant t
    )
    {
        _invoiceRepo = invoiceRepo;
        _creditNoteRepo = creditNoteRepo;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<PurchaseInvoiceTaxSummaryDto>>> Handle(
        GetPurchaseInvoiceTaxSummariesQuery q,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;

        var invoice = await _invoiceRepo.GetByIdAsync(tid, q.PurchaseInvoiceId, ct);
        if (invoice is null)
            return Result<IReadOnlyList<PurchaseInvoiceTaxSummaryDto>>.NotFound(
                "Factura de compra no encontrada."
            );

        var sourceIds = invoice.TaxSummaries.Select(s => s.Id).ToList();
        var creditedBySourceId =
            await _creditNoteRepo.GetCreditedTaxableBaseByPurchaseTaxSummaryIdsAsync(
                tid,
                sourceIds,
                excludePurchaseCreditNoteId: null,
                ct
            );

        var result = invoice
            .TaxSummaries.Select(s =>
            {
                var credited = creditedBySourceId.GetValueOrDefault(s.Id);
                return new PurchaseInvoiceTaxSummaryDto(
                    s.Id,
                    s.VatCode,
                    s.VatRate,
                    s.VatName,
                    s.IceCode,
                    s.IceRate,
                    s.IceName,
                    s.IrbpnrCode,
                    s.IrbpnrRate,
                    s.IrbpnrName,
                    s.TaxableBase,
                    s.IceAmount,
                    s.VatAmount,
                    s.IrbpnrAmount,
                    s.TotalAmount,
                    credited,
                    s.TaxableBase - credited
                );
            })
            .ToList();

        return Result<IReadOnlyList<PurchaseInvoiceTaxSummaryDto>>.Success(result);
    }
}
