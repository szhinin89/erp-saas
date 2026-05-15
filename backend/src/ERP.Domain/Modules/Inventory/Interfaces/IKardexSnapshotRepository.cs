using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IKardexSnapshotRepository
{
    /// <summary>
    /// Retorna el snapshot más reciente cuya <c>FechaSnapshot</c> sea ≤ <paramref name="hastaUtc"/>.
    /// Nulo si no existe ningún snapshot anterior a esa fecha.
    /// </summary>
    Task<KardexSnapshot?> GetLatestBeforeAsync(
        Guid     tenantId,
        Guid     productoId,
        Guid     bodegaId,
        DateTime hastaUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Inserta o reemplaza el snapshot del día indicado (UPSERT por tenant+producto+bodega+fecha).
    /// </summary>
    Task UpsertAsync(KardexSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Combinaciones distintas (producto, bodega) con movimientos en el tenant.
    /// Usada por el worker para saber qué calcular.
    /// </summary>
    Task<IReadOnlyList<(Guid ProductoId, Guid BodegaId)>> GetDistinctProductoBodegaAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>Lista de tenants que tienen al menos un movimiento de inventario.</summary>
    Task<IReadOnlyList<Guid>> GetTenantsConMovimientosAsync(CancellationToken ct = default);
}
