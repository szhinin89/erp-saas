using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Fiscal.Mapping;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Fiscal.Interfaces;

namespace ERP.Application.Sales.UseCases.GetSaleById;

public sealed class GetSaleByIdQueryHandler
    : IRequestHandler<GetSaleByIdQuery, Result<SalesBillDetailDto?>>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public GetSaleByIdQueryHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Result<SalesBillDetailDto?>> Handle(GetSaleByIdQuery query, CancellationToken ct)
    {
        var invoice = await _invoiceRepository.GetByPublicIdAsync(query.Id, ct);
        if (invoice is null)
            return Result<SalesBillDetailDto?>.Success(null);

        return Result<SalesBillDetailDto?>.Success(InvoiceDtoMapper.ToDetailDto(invoice));
    }
}
