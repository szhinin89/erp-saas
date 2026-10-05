using System.Globalization;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Secuencial interno mínimo de <see cref="PurchaseReturn.ReturnNumber"/> — diseño §7.1bis.
/// Deliberadamente <em>no</em> es una segunda <c>DocumentSequence</c>: ámbito
/// <c>(TenantId, CompanyId)</c> sin <c>EmissionPointId</c>/<c>DocTypeCode</c> (una devolución de
/// compra no emite ningún comprobante SRI propio, §3.13), formato <c>D8</c> (deliberadamente
/// distinto del <c>D9</c> de <c>DocumentSequence</c>, para que nunca sea confundible con un número
/// SRI). La captura atómica real (<c>pg_advisory_xact_lock</c> dentro de la transacción ambiente de
/// <c>Authorize()</c>, nunca transacción propia) es responsabilidad de
/// <c>IPurchaseReturnSequenceRepository.CaptureNextAsync</c> — Fase 2, fuera del alcance de esta
/// entidad de dominio puro.
/// </summary>
public sealed class PurchaseReturnSequence
    : BaseEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }

    /// <summary>Próximo número a emitir. Empieza en 1 para que el primer documento sea "00000001".</summary>
    public int CurrentSeq { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private PurchaseReturnSequence() { }

    public static PurchaseReturnSequence Create(Guid tenantId, Guid companyId)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));

        var now = DateTime.UtcNow;
        return new PurchaseReturnSequence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            CurrentSeq = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Devuelve el secuencial actual formateado (D8) e incrementa el contador en memoria.
    /// PRECONDICIÓN: usar únicamente desde <c>IPurchaseReturnSequenceRepository.CaptureNextAsync</c>
    /// (Fase 2), que garantiza el advisory lock transaccional y la participación en la misma
    /// transacción de <c>Authorize()</c> (§7.1bis).
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
        return formatted;
    }
}
