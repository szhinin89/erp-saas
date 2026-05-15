using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.GetVentaById;

public sealed class GetSaleByIdQueryHandler
    : IRequestHandler<GetSaleByIdQuery, Result<SalesBillDetailDto?>>
{
    private readonly ISalesRepository _ventasRepository;
    private readonly ICurrentTenant    _currentTenant;

    public GetSaleByIdQueryHandler(ISalesRepository ventasRepository, ICurrentTenant currentTenant)
    {
        _ventasRepository = ventasRepository;
        _currentTenant    = currentTenant;
    }

    public async Task<Result<SalesBillDetailDto?>> Handle(GetSaleByIdQuery query, CancellationToken ct)
    {
        var factura = await _ventasRepository.GetBillByIdAsync(_currentTenant.TenantId, query.Id, ct);
        if (factura is null)
            return Result<SalesBillDetailDto?>.Success(null);

        var detalles = factura.Lines.Select(d => new SalesDetailDto(
            d.Id, d.ProductId, d.Description,
            d.Quantity, d.UnitPrice,
            d.Subtotal, d.VatTotal, d.Total)).ToList();

        return Result<SalesBillDetailDto?>.Success(new SalesBillDetailDto(
            factura.Id,
            factura.CustomerId,
            factura.Cliente?.LegalName ?? factura.CustomerId.ToString(),
            factura.WarehouseId,
            factura.BranchId,
            factura.DocType,
            factura.EstabCode,
            factura.EmPointCode,
            factura.Sequential,
            factura.AccessKey,
            factura.IssueDate,
            factura.Subtotal,
            factura.VatTotal,
            factura.Total,
            factura.Status,
            factura.AuthNumber,
            factura.AuthDate,
            factura.XmlSignedPath,
            factura.XmlAuthPath,
            factura.ErrorMessage,
            factura.JournalEntryId,
            factura.CreatedAt,
            detalles));
    }
}
