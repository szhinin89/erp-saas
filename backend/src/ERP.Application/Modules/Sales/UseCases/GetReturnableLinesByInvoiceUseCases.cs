using ERP.Application.Common;
using ERP.Application.Modules.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Sales.UseCases;

public sealed record GetReturnableLinesByInvoiceQuery(Guid InvoiceId)
    : IRequest<Result<IReadOnlyList<ReturnableLineDto>>>,
        IBranchScopedRequest;

public sealed class GetReturnableLinesByInvoiceHandler
    : IRequestHandler<GetReturnableLinesByInvoiceQuery, Result<IReadOnlyList<ReturnableLineDto>>>
{
    private readonly ISalesInvoiceRepository _invoiceRepo;
    private readonly ISalesReturnRepository _returnRepo;
    private readonly ICurrentTenant _t;
    private readonly ICurrentBranch _b;

    public GetReturnableLinesByInvoiceHandler(
        ISalesInvoiceRepository invoiceRepo,
        ISalesReturnRepository returnRepo,
        ICurrentTenant t,
        ICurrentBranch b
    )
    {
        _invoiceRepo = invoiceRepo;
        _returnRepo = returnRepo;
        _t = t;
        _b = b;
    }

    public async Task<Result<IReadOnlyList<ReturnableLineDto>>> Handle(
        GetReturnableLinesByInvoiceQuery q,
        CancellationToken ct
    )
    {
        var invoice = await _invoiceRepo.GetByIdAsync(_t.TenantId, q.InvoiceId, ct);
        if (invoice is null || invoice.BranchId != _b.BranchId)
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
                    remaining * line.ConversionFactor,
                    line.BaseUomCode
                )
            );
        }

        return Result<IReadOnlyList<ReturnableLineDto>>.Success(result);
    }
}
