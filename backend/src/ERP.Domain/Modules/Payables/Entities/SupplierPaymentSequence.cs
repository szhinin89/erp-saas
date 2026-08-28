using ERP.Domain.Common;
using System.Globalization;

namespace ERP.Domain.Modules.Payables.Entities;

/// <summary>
/// SUPPLIER-PAYMENTS-FOUNDATION-15B — secuencial interno de <see cref="SupplierPayment.SystemNumber"/>,
/// siguiendo el mismo patrón que <c>PurchaseReturnSequence</c> (SUPPLIER-PAYMENTS-AUDIT-15A §3):
/// deliberadamente NO es un uso más de <c>DocumentSequence</c> (infraestructura FROZEN) — un pago a
/// proveedor no emite ningún comprobante SRI propio, y <c>DocumentSequence.CaptureNextAsync</c> está
/// atado a <c>EmissionPointId</c>/<c>DocTypeCode</c> SRI, que no aplica aquí. Ámbito
/// <c>(TenantId, CompanyId)</c>, formato <c>D8</c> (igual que <c>PurchaseReturnSequence</c>, para no
/// ser confundible con el <c>D9</c> de <c>DocumentSequence</c>), con <see cref="Prefix"/> opcional
/// antepuesto (p. ej. <c>"PP-00000001"</c>). La captura atómica real
/// (<c>pg_advisory_xact_lock</c>) es responsabilidad de
/// <c>ISupplierPaymentSequenceRepository.CaptureNextAsync</c> — fuera del alcance de esta entidad de
/// dominio puro.
/// </summary>
public sealed class SupplierPaymentSequence : BaseEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public const int PrefixMaxLen = 10;

    public Guid CompanyId { get; private set; }

    /// <summary>Próximo número a emitir. Empieza en 1 para que el primer documento sea "00000001".</summary>
    public int CurrentSeq { get; private set; }
    public string? Prefix { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SupplierPaymentSequence() { }

    public static SupplierPaymentSequence Create(Guid tenantId, Guid companyId, string? prefix = null)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));

        var now = DateTime.UtcNow;
        return new SupplierPaymentSequence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            CurrentSeq = 1,
            Prefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Devuelve el secuencial actual formateado (D8, con <see cref="Prefix"/> antepuesto si existe)
    /// e incrementa el contador en memoria. PRECONDICIÓN: usar únicamente desde
    /// <c>ISupplierPaymentSequenceRepository.CaptureNextAsync</c>, que garantiza el advisory lock
    /// transaccional.
    /// </summary>
    public string CaptureAndIncrement()
    {
        if (CurrentSeq < 1)
            throw new InvalidOperationException(
                $"Invariante violada: CurrentSeq debe ser ≥ 1 pero es {CurrentSeq}."
            );

        var formatted = CurrentSeq.ToString("D8", CultureInfo.InvariantCulture);
        CurrentSeq++;
        UpdatedAt = DateTime.UtcNow;
        return Prefix is null ? formatted : $"{Prefix}-{formatted}";
    }
}
