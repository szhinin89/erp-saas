namespace ERP.Domain.Modules.Payables.Interfaces;

/// <summary>
/// Contrato de la infraestructura de numeración interna de <c>SupplierPayment.SystemNumber</c> —
/// mismo patrón que <c>IPurchaseReturnSequenceRepository</c> (SUPPLIER-PAYMENTS-AUDIT-15A §3).
/// </summary>
public interface ISupplierPaymentSequenceRepository
{
    /// <summary>
    /// Captura y reserva atómicamente el siguiente número secuencial para
    /// <paramref name="tenantId"/>/<paramref name="companyId"/>. Debe invocarse dentro de la
    /// transacción ambiente del caso de uso de confirmación — nunca abre ni confirma una
    /// transacción propia. Crea la fila de secuencia on-demand si no existe.
    /// </summary>
    /// <returns>El número formateado (8 dígitos, con prefijo si está configurado), ej. "00000001" o "PP-00000001".</returns>
    Task<string> CaptureNextAsync(Guid tenantId, Guid companyId, CancellationToken ct = default);
}
