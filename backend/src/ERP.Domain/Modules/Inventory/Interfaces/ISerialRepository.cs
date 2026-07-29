using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface ISerialRepository
{
    Task<SerialNumber?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SerialNumber?> GetBySerialAsync(
        Guid itemId,
        string serial,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<SerialNumber>> GetByItemAsync(
        Guid itemId,
        Guid? warehouseId = null,
        SerialStatus? status = null,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<SerialNumber>> GetByLotAsync(
        Guid lotId,
        CancellationToken cancellationToken = default
    );

    Task<bool> ExistsAsync(
        Guid itemId,
        string serial,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(SerialNumber serial, CancellationToken cancellationToken = default);

    /// <summary>Alta masiva: registra múltiples seriales de una recepción en una sola operación.</summary>
    Task AddRangeAsync(
        IEnumerable<SerialNumber> serials,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
