using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Interfaces;
using MediatR;

namespace ERP.Application.Items.UseCases.ResolveItem;

public sealed record ResolveItemQuery(string Code)
    : IRequest<Result<ItemDto?>>, ICompanyScopedRequest;

public sealed class ResolveItemQueryHandler : IRequestHandler<ResolveItemQuery, Result<ItemDto?>>
{
    private readonly IItemRepository _repo;
    private readonly ICurrentTenant _t;
    private readonly ISriCatalogResolver _sri;
    private readonly IItemTypeRepository _itemTypeRepo;

    public ResolveItemQueryHandler(
        IItemRepository repo, ICurrentTenant t, ISriCatalogResolver sri, IItemTypeRepository itemTypeRepo)
    { _repo = repo; _t = t; _sri = sri; _itemTypeRepo = itemTypeRepo; }

    public async Task<Result<ItemDto?>> Handle(ResolveItemQuery q, CancellationToken ct)
    {
        var code = q.Code.Trim();
        if (string.IsNullOrEmpty(code))
            return Result<ItemDto?>.ValidationFailure("El código de búsqueda es obligatorio.");

        var item = await _repo.ResolveByAnyCodeAsync(code, _t.TenantId, ct);
        if (item is null)
            return Result<ItemDto?>.Success(null);

        var uomMap = await _sri.ResolveUomsAsync([item.DefaultUomCode], ct);
        var itemType = await _itemTypeRepo.GetByIdAsync(_t.TenantId, item.ItemTypeId, ct);
        var itemTypeNames = itemType is null
            ? new Dictionary<Guid, string>()
            : new Dictionary<Guid, string> { [itemType.Id] = itemType.Name };
        return Result<ItemDto?>.Success(ItemMappingService.ToDto(item, uomMap, itemTypeNames));
    }
}
