using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.GetVentasList;

public sealed class GetVentasListQueryHandler
    : IRequestHandler<GetVentasListQuery, Result<VentasPagedResult>>
{
    private readonly ISalesRepository _ventasRepository;
    private readonly ICurrentTenant    _currentTenant;

    public GetVentasListQueryHandler(ISalesRepository ventasRepository, ICurrentTenant currentTenant)
    {
        _ventasRepository = ventasRepository;
        _currentTenant    = currentTenant;
    }

    public async Task<Result<VentasPagedResult>> Handle(GetVentasListQuery query, CancellationToken ct)
    {
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize   = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await _ventasRepository.GetBillsPagedAsync(
            _currentTenant.TenantId,
            pageNumber,
            pageSize,
            query.CustomerId,
            query.DateFrom,
            query.DateTo,
            query.Status,
            query.Search,
            ct);

        var dtos = items.Select(ToDto).ToList();
        return Result<VentasPagedResult>.Success(new VentasPagedResult(dtos, totalCount, pageNumber, pageSize));
    }

    private static SalesBillDto ToDto(SalesBill f) => new(
        f.Id,
        f.CustomerId,
        f.Cliente?.LegalName ?? f.CustomerId.ToString(),
        f.WarehouseId,
        f.BranchId,
        f.EstabCode,
        f.EmPointCode,
        f.Sequential,
        f.AccessKey,
        f.IssueDate,
        f.Subtotal,
        f.VatTotal,
        f.Total,
        f.Status,
        f.AuthNumber,
        f.AuthDate,
        f.ErrorMessage,
        f.JournalEntryId,
        f.CreatedAt);
}
