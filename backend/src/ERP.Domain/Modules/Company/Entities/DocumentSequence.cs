using ERP.Domain.Common;
using System.Globalization;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Controla el secuencial por punto de emisión y tipo de comprobante SRI.
/// Usar <see cref="IDocumentSequenceRepository.CaptureNextAsync"/> para obtener el siguiente número —
/// esa operación es atómica (advisory lock + transacción explícita) y garantiza unicidad bajo concurrencia.
/// </summary>
public sealed class DocumentSequence : BaseEntity, ITenantScopedEntity, ICompanyScopedEntity
{
    /// <summary>El secuencial SRI se formatea D9 — 9 dígitos es el máximo representable.</summary>
    public const int MaxSequentialValue = 999_999_999;

    public Guid CompanyId { get; private set; }
    public Guid EmissionPointId { get; private set; }

    /// <summary>Código de tipo documental SRI: "01" Factura, "04" NC, "05" ND, "07" Retención.</summary>
    public string DocTypeCode { get; private set; } = null!;

    /// <summary>Próximo número a emitir. Empieza en 1 para que el primer documento sea "000000001".</summary>
    public int CurrentSeq { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// DOCUMENT-SEQUENCES-CONFIG-03 — true si esta secuencia ya entregó al menos un número real
    /// vía <see cref="CaptureAndIncrement"/>. Distingue "configurada pero nunca capturada" (se
    /// puede seguir reconfigurando libremente) de "ya usada" (el número inicial queda fijo salvo
    /// ajuste restringido, fuera de alcance de esta fase — ver
    /// <c>docs/decisions/DOCUMENT-SEQUENCES-CONFIG-03.md</c>).
    /// No se deriva de <c>CurrentSeq > 1</c> porque <see cref="ConfigureNextNumber"/> también deja
    /// <c>CurrentSeq > 1</c> sin que haya existido ninguna captura real.
    /// </summary>
    public bool HasBeenUsed { get; private set; }

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
            HasBeenUsed = false,
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
        HasBeenUsed = true;
        UpdatedAt = DateTime.UtcNow;
        return formatted;
    }

    /// <summary>
    /// DOCUMENT-SEQUENCES-CONFIG-03 — fija el próximo secuencial a entregar, para empresas que
    /// migran numeración SRI ya en curso desde otro sistema (p. ej. última retención emitida
    /// "001-001-000000849" → configurar <paramref name="nextNumber"/> = 850 para que la primera
    /// retención del ERP sea "001-001-000000850").
    ///
    /// Solo permitido si la secuencia nunca entregó un número real (<see cref="HasBeenUsed"/> es
    /// <c>false</c>) — una vez que existe al menos una captura real, el ajuste libre queda fuera
    /// de esta fase (ver docs/decisions/DOCUMENT-SEQUENCES-CONFIG-03.md § reglas de ajuste
    /// restringido, no implementado todavía).
    /// </summary>
    /// <exception cref="InvalidOperationException">La secuencia ya entregó al menos un número real.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nextNumber"/> no es positivo o excede 9 dígitos.</exception>
    public void ConfigureNextNumber(int nextNumber)
    {
        if (HasBeenUsed)
            throw new InvalidOperationException(
                "Esta secuencia ya entregó al menos un número real; no se puede reconfigurar "
                    + "libremente. El ajuste posterior a la primera captura requiere permiso "
                    + "especial, motivo y auditoría — fuera de alcance de esta fase "
                    + "(DOCUMENT-SEQUENCES-CONFIG-03)."
            );

        if (nextNumber < 1)
            throw new ArgumentOutOfRangeException(
                nameof(nextNumber),
                nextNumber,
                "El siguiente secuencial debe ser mayor o igual a 1."
            );

        if (nextNumber > MaxSequentialValue)
            throw new ArgumentOutOfRangeException(
                nameof(nextNumber),
                nextNumber,
                $"El siguiente secuencial no puede superar {MaxSequentialValue} (9 dígitos)."
            );

        CurrentSeq = nextNumber;
        UpdatedAt = DateTime.UtcNow;
    }
}
