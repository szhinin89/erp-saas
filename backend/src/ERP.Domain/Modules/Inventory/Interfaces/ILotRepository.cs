using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface ILotRepository
{
    Task<Lot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Lot?> GetByLotNumberAsync(
        Guid itemId,
        string lotNumber,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Lot>> GetByItemAsync(
        Guid itemId,
        Guid? warehouseId = null,
        LotStatus? status = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Lotes con ExpirationDate antes de la fecha indicada, en estado Active.</summary>
    Task<IReadOnlyList<Lot>> GetExpiringBeforeAsync(
        DateOnly expirationDate,
        CancellationToken cancellationToken = default
    );

    Task<bool> ExistsAsync(
        Guid itemId,
        string lotNumber,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(Lot lot, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
