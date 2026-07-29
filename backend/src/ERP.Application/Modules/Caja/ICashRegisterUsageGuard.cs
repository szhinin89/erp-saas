namespace ERP.Application.Modules.Caja;

/// <summary>
/// Regla de negocio centralizada de mutabilidad de <c>CashRegister</c>: una Caja utilizada en
/// al menos un proceso operativo (hoy: <c>CashSession</c> — Aperturas/Cierres/Movimientos/Arqueos
/// cuelgan todos de una sesión) pasa a formar parte de la trazabilidad histórica del ERP y su
/// identidad operacional (Punto de Emisión) ya no puede reasignarse. Único punto de verdad — nunca
/// duplicar esta verificación en Controllers, otros Handlers o el frontend.
/// </summary>
public interface ICashRegisterUsageGuard
{
    /// <summary>True si la Caja tiene al menos un registro operativo asociado (uso histórico permanente).</summary>
    Task<bool> HasHistoryAsync(Guid tenantId, Guid cashRegisterId, CancellationToken ct = default);

    /// <summary>True si la Caja tiene una sesión actualmente abierta (bloquea la desactivación).</summary>
    Task<bool> HasOpenSessionAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken ct = default
    );

    /// <summary>Versión masiva de <see cref="HasHistoryAsync"/> para proyecciones de listado.</summary>
    Task<IReadOnlyCollection<Guid>> GetUsedIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> cashRegisterIds,
        CancellationToken ct = default
    );
}
