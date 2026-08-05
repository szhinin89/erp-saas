using ERP.Application.Common;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Domain.Modules.Purchases.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Purchases.UseCases;

/// <summary>
/// P0-02 Fase 5 — líneas devolvibles de una <c>PurchaseInvoice</c> (diseño §10.2, §14.2): por
/// línea, cantidad original, ya devuelta (solo devoluciones <c>Authorized</c>), remanente y
/// <c>WarehouseId</c> congelado — solo lectura, nunca seleccionable por el cliente.
/// </summary>
public sealed record GetReturnableLinesByPurchaseInvoiceQuery(Guid PurchaseInvoiceId)
    : IRequest<Result<IReadOnlyList<ReturnableLineDto>>>,
        IBranchScopedRequest;

public sealed class GetReturnableLinesByPurchaseInvoiceHandler
    : IRequestHandler<
        GetReturnableLinesByPurchaseInvoiceQuery,
        Result<IReadOnlyList<ReturnableLineDto>>
    >
{
    private readonly IPurchaseInvoiceRepository _invoiceRepo;
    private readonly IPurchaseReturnRepository _returnRepo;
    private readonly ICurrentTenant _t;

    public GetReturnableLinesByPurchaseInvoiceHandler(
        IPurchaseInvoiceRepository invoiceRepo,
        IPurchaseReturnRepository returnRepo,
        ICurrentTenant t
    )
    {
        _invoiceRepo = invoiceRepo;
        _returnRepo = returnRepo;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<ReturnableLineDto>>> Handle(
        GetReturnableLinesByPurchaseInvoiceQuery q,
        CancellationToken ct
    )
    {
        var tid = _t.TenantId;

        var invoice = await _invoiceRepo.GetByIdAsync(tid, q.PurchaseInvoiceId, ct);
        if (invoice is null)
            return Result<IReadOnlyList<ReturnableLineDto>>.NotFound(
                "Factura de compra no encontrada."
            );

        var eligibleLines = invoice.Lines.Where(l => l.ItemId is not null).ToList();
        var detailIds = eligibleLines.Select(l => l.Id).ToList();
        var returnedByDetailId = await _returnRepo.GetReturnedQuantitiesByInvoiceDetailIdsAsync(
            tid,
            detailIds,
            ct
        );

        var result = eligibleLines
            .Select(l =>
            {
                var warehouseId = l.WarehouseId ?? invoice.GlobalWarehouseId ?? Guid.Empty;
                var returned = returnedByDetailId.GetValueOrDefault(l.Id);
                return new ReturnableLineDto(
                    l.Id,
                    l.ItemId!.Value,
                    l.Description,
                    l.Quantity,
                    returned,
                    l.Quantity - returned,
                    warehouseId
                );
            })
            .ToList();

        return Result<IReadOnlyList<ReturnableLineDto>>.Success(result);
    }
}
