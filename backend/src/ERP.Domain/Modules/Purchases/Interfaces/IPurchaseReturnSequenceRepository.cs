using ERP.Domain.Modules.Purchases.Entities;

namespace ERP.Domain.Modules.Purchases.Interfaces;

/// <summary>
/// Contrato de la infraestructura de numeración interna de <see cref="PurchaseReturn.ReturnNumber"/>
/// (diseño §7.1bis) — solo la firma se declara en esta fase (Fase 1, dominio puro); la
/// implementación real (<c>pg_advisory_xact_lock</c> con namespace <c>"PurchaseReturn.Sequence"</c>,
/// dentro de la transacción ambiente ya abierta por el caller, nunca transacción propia) es Fase 2.
/// </summary>
public interface IPurchaseReturnSequenceRepository
{
    /// <summary>
    /// Captura y reserva atómicamente el siguiente número secuencial para
    /// <paramref name="tenantId" />/<paramref name="companyId" />. Debe invocarse dentro de la
    /// transacción ambiente ya abierta por <c>AuthorizePurchaseReturnUseCases</c> — nunca abre ni
    /// confirma una transacción propia. Crea la fila de secuencia on-demand si no existe.
    /// </summary>
    /// <returns>El número formateado en 8 dígitos, ej. "00000001".</returns>
    Task<string> CaptureNextAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);
}
