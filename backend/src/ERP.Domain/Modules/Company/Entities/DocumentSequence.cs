using System.Globalization;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Controla el secuencial por punto de emisión y tipo de comprobante SRI.
/// Usar <see cref="IDocumentSequenceRepository.CaptureNextAsync"/> para obtener el siguiente número —
/// esa operación es atómica (advisory lock + transacción explícita) y garantiza unicidad bajo concurrencia.
/// </summary>
public sealed class DocumentSequence : BaseEntity, ITenantScopedEntity, ICompanyScopedEntity
{
    public Guid CompanyId { get; private set; }
    public Guid EmissionPointId { get; private set; }

    /// <summary>Código de tipo documental SRI: "01" Factura, "04" NC, "05" ND, "07" Retención.</summary>
    public string DocTypeCode { get; private set; } = null!;

    /// <summary>Próximo número a emitir. Empieza en 1 para que el primer documento sea "000000001".</summary>
    public int CurrentSeq { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private DocumentSequence() { }

    public static DocumentSequence Create(
        Guid tenantId,
        Guid companyId,
        Guid emissionPointId,
        string docTypeCode
    )
    {
        if (string.IsNullOrWhiteSpace(docTypeCode))
            throw new ArgumentException(
                "El código de tipo documental es obligatorio.",
                nameof(docTypeCode)
            );

        var now = DateTime.UtcNow;
        return new DocumentSequence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            EmissionPointId = emissionPointId,
            DocTypeCode = docTypeCode.Trim(),
            CurrentSeq = 1, // primer documento → "000000001" (SRI válido)
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Devuelve el secuencial actual formateado (D9) e incrementa el contador en memoria.
    /// PRECONDICIÓN: usar únicamente desde <see cref="IDocumentSequenceRepository.CaptureNextAsync"/>
    /// que garantiza el bloqueo y la transacción.
    /// </summary>
    public string CaptureAndIncrement()
    {
        // Invariante: CurrentSeq nunca puede ser < 1. Si llega aquí en ese estado
        // la BD ya debería haberlo rechazado vía chk_doc_seq_positive.
        if (CurrentSeq < 1)
            throw new InvalidOperationException(
                $"Invariante violada: CurrentSeq debe ser ≥ 1 pero es {CurrentSeq}."
            );

        var formatted = CurrentSeq.ToString("D9", CultureInfo.InvariantCulture);
        CurrentSeq++;
        UpdatedAt = DateTime.UtcNow;
        return formatted;
    }
}
