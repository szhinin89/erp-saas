using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface ILotRepository
{
    Task<Lot?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Lot?> GetByLotNumberAsync(
        Guid itemId, string lotNumber, CancellationToken ct = default);

    Task<IReadOnlyList<Lot>> GetByItemAsync(
        Guid itemId,
        Guid? warehouseId = null,
        LotStatus? status = null,
        CancellationToken ct = default);

    /// <summary>Lotes con ExpirationDate antes de la fecha indicada, en estado Active.</summary>
    Task<IReadOnlyList<Lot>> GetExpiringBeforeAsync(
        DateOnly date, CancellationToken ct = default);

    Task<bool> ExistsAsync(
        Guid itemId, string lotNumber, CancellationToken ct = default);

    Task AddAsync(Lot lot, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
