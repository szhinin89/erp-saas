using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Items;

public sealed class ItemTypeRepository : IItemTypeRepository
{
    private readonly ErpDbContext _db;

    public ItemTypeRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<ItemTypeDefinition>> ListAsync(
        Guid tenantId,
        bool onlyActive = true,
        CancellationToken ct = default
    )
    {
        var q = _db.ItemTypes.Where(x => x.TenantId == tenantId);

        if (onlyActive)
            q = q.Where(x => x.IsActive);

        return await q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).AsNoTracking().ToListAsync(ct);
    }

    public async Task<ItemTypeDefinition?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) => await _db.ItemTypes.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    // NOTA: a diferencia de PaymentMethod, el código NO se normaliza a mayúsculas —
    // los 5 valores seed (Physical, Service, Digital, Kit, Bundle) deben coincidir
    // exactamente con los valores ya persistidos en items.item_type (ex-enum ToString()).
    public async Task<ItemTypeDefinition?> GetByCodeAsync(
        Guid tenantId,
        string code,
        CancellationToken ct = default
    )
    {
        var trimmed = code.Trim();
        return await _db.ItemTypes.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Code == trimmed,
            ct
        );
    }

    public async Task<bool> ExistsByCodeAsync(
        Guid tenantId,
        string code,
        Guid? excludeId = null,
        CancellationToken ct = default
    )
    {
        var trimmed = code.Trim();
        var q = _db.ItemTypes.Where(x => x.TenantId == tenantId && x.Code == trimmed);
        if (excludeId.HasValue)
            q = q.Where(x => x.Id != excludeId.Value);
        return await q.AnyAsync(ct);
    }

    // NOTA: items.item_type_id es ahora una FK a item_types.id (ya no guarda el código).
    // Se resuelve el Id del tipo por código y luego se filtran los ítems por ese Id.
    public async Task<bool> HasActiveItemsAsync(
        Guid tenantId,
        string code,
        CancellationToken ct = default
    )
    {
        var itemType = await GetByCodeAsync(tenantId, code, ct);
        if (itemType is null)
            return false;

        return await _db.Items.AnyAsync(
            x => x.TenantId == tenantId && x.IsActive && x.ItemTypeId == itemType.Id,
            ct
        );
    }

    public async Task AddAsync(ItemTypeDefinition entity, CancellationToken ct = default) =>
        await _db.ItemTypes.AddAsync(entity, ct);

    public void Update(ItemTypeDefinition entity) => _db.ItemTypes.Update(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct);
}
