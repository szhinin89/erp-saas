using MediatR;
using ERP.Application.Common;
using ERP.Application.SupplierCatalog.DTOs;
using ERP.Domain.Modules.SupplierCatalog.Interfaces;

namespace ERP.Application.SupplierCatalog.UseCases.GetItemSuppliers;

public sealed record GetItemSuppliersQuery(Guid ItemId)
    : IRequest<Result<IReadOnlyList<SupplierItemDto>>>, ICompanyScopedRequest;

public sealed class GetItemSuppliersQueryHandler
    : IRequestHandler<GetItemSuppliersQuery, Result<IReadOnlyList<SupplierItemDto>>>
{
    private readonly ISupplierItemRepository _repository;

    public GetItemSuppliersQueryHandler(ISupplierItemRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<SupplierItemDto>>> Handle(
        GetItemSuppliersQuery query, CancellationToken ct)
    {
        var items = await _repository.GetByItemAsync(query.ItemId, ct);

        var dtos = items.Select(si => new SupplierItemDto(
            si.Id, si.SupplierId, si.ItemId, si.VariantId,
            si.SupplierCode, si.SupplierName, si.DefaultUomCode,
            si.LeadTimeDays, si.MinOrderQty, si.IsDefault, si.IsActive,
            si.Prices.Select(p => new SupplierPriceDto(
                p.Id, p.MinQty, p.UnitPrice, p.CurrencyCode, p.ValidFrom, p.ValidTo)).ToList())).ToList();

        return Result<IReadOnlyList<SupplierItemDto>>.Success(dtos);
    }
}
