namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Contrato para el recálculo de snapshots del kardex.
/// Implementado en Infrastructure por <c>KardexSnapshotService</c>.
/// </summary>
public interface IKardexSnapshotCalculator
{
    /// <summary>Recalcula snapshots para un tenant, con filtros opcionales.</summary>
    Task<int> RecalcularSubscriberAsync(
        Guid      subscriberId,
        Guid?     productoId,
        Guid?     bodegaId,
        DateTime  untilDate,
        CancellationToken ct = default);

    /// <summary>Recalcula snapshots para todos los subscribers hasta la fecha indicada.</summary>
    Task<int> RecalcularTodosAsync(DateTime untilDate, CancellationToken ct = default);
}
