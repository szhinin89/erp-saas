using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface ISerialRepository
{
    Task<SerialNumber?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<SerialNumber?> GetBySerialAsync(
        Guid itemId, string serial, CancellationToken ct = default);

    Task<IReadOnlyList<SerialNumber>> GetByItemAsync(
        Guid itemId,
        Guid? warehouseId = null,
        SerialStatus? status = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<SerialNumber>> GetByLotAsync(
        Guid lotId, CancellationToken ct = default);

    Task<bool> ExistsAsync(
        Guid itemId, string serial, CancellationToken ct = default);

    Task AddAsync(SerialNumber serial, CancellationToken ct = default);

    /// <summary>Alta masiva: registra múltiples seriales de una recepción en una sola operación.</summary>
    Task AddRangeAsync(IEnumerable<SerialNumber> serials, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
