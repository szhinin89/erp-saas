using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.GetItemById;

public sealed record GetItemByIdQuery(Guid Id)
    : IRequest<Result<ItemDetailDto>>, ICompanyScopedRequest;

public sealed class GetItemByIdQueryHandler
    : IRequestHandler<GetItemByIdQuery, Result<ItemDetailDto>>
{
    private readonly IItemRepository     _repository;
    private readonly ICurrentTenant      _currentTenant;
    private readonly ISriCatalogResolver _sri;
    private readonly IItemTypeRepository _itemTypeRepo;

    public GetItemByIdQueryHandler(
        IItemRepository repository,
        ICurrentTenant tenant,
        ISriCatalogResolver sri,
        IItemTypeRepository itemTypeRepo)
    {
        _repository    = repository;
        _currentTenant = tenant;
        _sri           = sri;
        _itemTypeRepo  = itemTypeRepo;
    }

    public async Task<Result<ItemDetailDto>> Handle(GetItemByIdQuery query, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(query.Id, _currentTenant.TenantId, cancellationToken);
        if (item is null)
            return Result<ItemDetailDto>.NotFound("Ítem no encontrado.");

        // Collect all UOM codes used by this item in one batch query
        var uomCodes = item.UnitConversions
            .SelectMany(c => new[] { c.FromUomCode, c.ToUomCode })
            .Append(item.DefaultUomCode)
            .Concat(item.PackagingLevels.Select(p => p.UomCode));

        var vatCodes = new[] { item.TaxConfig.SaleVatCode, item.TaxConfig.PurchaseVatCode }
            .Where(c => !string.IsNullOrWhiteSpace(c)).Cast<string>();

        var iceCodes = !string.IsNullOrWhiteSpace(item.TaxConfig.ExciseTaxCode)
            ? new[] { item.TaxConfig.ExciseTaxCode }
            : [];

        var uomMap = await _sri.ResolveUomsAsync(uomCodes, cancellationToken);
        var vatMap = await _sri.ResolveVatRatesAsync(vatCodes, cancellationToken);
        var iceMap = await _sri.ResolveIceRatesAsync(iceCodes, cancellationToken);

        var itemType = await _itemTypeRepo.GetByIdAsync(_currentTenant.TenantId, item.ItemTypeId, cancellationToken);
        var itemTypeNames = itemType is null
            ? new Dictionary<Guid, string>()
            : new Dictionary<Guid, string> { [itemType.Id] = itemType.Name };

        return Result<ItemDetailDto>.Success(
            ItemMappingService.ToDetailDto(item, uomMap, vatMap, iceMap, itemTypeNames));
    }
}
