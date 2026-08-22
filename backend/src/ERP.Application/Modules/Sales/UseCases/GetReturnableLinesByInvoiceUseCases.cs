using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

/// <summary>
/// Fase I-6B: branch-scoped por exigencia de contexto (defensa en profundidad) — mismo criterio
/// que <c>GetReceivableByInvoiceQuery</c>: <c>SalesInvoice</c> no tiene <c>BranchId</c> de
/// cabecera, así que esto no filtra resultados por sucursal, solo exige sucursal activa
/// autorizada.
/// </summary>
public sealed record GetReturnableLinesByInvoiceQuery(Guid InvoiceId)
    : IRequest<Result<IReadOnlyList<ReturnableLineDto>>>,
        IBranchScopedRequest;

public sealed class GetReturnableLinesByInvoiceHandler
    : IRequestHandler<GetReturnableLinesByInvoiceQuery, Result<IReadOnlyList<ReturnableLineDto>>>
{
    private readonly ISalesInvoiceRepository _invoiceRepo;
    private readonly ISalesReturnRepository _returnRepo;
    private readonly ICurrentTenant _t;

    public GetReturnableLinesByInvoiceHandler(
        ISalesInvoiceRepository invoiceRepo,
        ISalesReturnRepository returnRepo,
        ICurrentTenant t
    )
    {
        _invoiceRepo = invoiceRepo;
        _returnRepo = returnRepo;
        _t = t;
    }

    public async Task<Result<IReadOnlyList<ReturnableLineDto>>> Handle(
        GetReturnableLinesByInvoiceQuery q,
        CancellationToken ct
    )
    {
        var invoice = await _invoiceRepo.GetByIdAsync(_t.TenantId, q.InvoiceId, ct);
        if (invoice is null)
            return Result<IReadOnlyList<ReturnableLineDto>>.NotFound("Factura no encontrada.");

        var result = new List<ReturnableLineDto>();
        foreach (var line in invoice.Lines)
        {
            var returned = await _returnRepo.GetReturnedQuantityByInvoiceDetailAsync(
                _t.TenantId,
                line.Id,
                ct
            );
            var remaining = line.Quantity - returned;
            result.Add(
                new ReturnableLineDto(
                    line.Id,
                    line.ItemId,
                    line.Description,
                    line.SnapshotSku,
                    line.WarehouseId,
                    line.UomCode,
                    line.Quantity,
                    returned,
                    remaining,
                    line.UnitPrice,
                    line.DiscountPct,
                    line.VatCode,
                    line.VatRate,
                    line.IceCode,
                    line.IceRate,
                    line.PackagingLevelId,
                    line.ConversionFactor,
                    line.QuantityInBaseUom,
                    remaining * line.ConversionFactor
                )
            );
        }

        return Result<IReadOnlyList<ReturnableLineDto>>.Success(result);
    }
}
