using ERP.Domain.Common;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Controla el secuencial por punto de emisión y tipo de comprobante SRI.
/// CRÍTICO: <see cref="CaptureAndIncrement"/> debe ejecutarse siempre dentro de una
/// transacción con <c>SELECT … FOR UPDATE</c> para evitar secuenciales duplicados.
/// </summary>
public sealed class DocumentSequence : BaseEntity, ICompanyScopedEntity
{
    public Guid     CompanyId       { get; private set; }
    public Guid     EmissionPointId { get; private set; }
    /// <summary>Código de tipo documental SRI: "01" Factura, "04" NC, "05" ND, "07" Retención.</summary>
    public string   DocTypeCode     { get; private set; } = null!;
    public int      CurrentSeq      { get; private set; } = 0;
    public DateTime UpdatedAt       { get; private set; }

    private DocumentSequence() { }

    public static DocumentSequence Create(
        Guid   subscriberId,
        Guid   companyId,
        Guid   emissionPointId,
        string docTypeCode)
    {
        if (string.IsNullOrWhiteSpace(docTypeCode))
            throw new ArgumentException("El código de tipo documental es obligatorio.", nameof(docTypeCode));

        return new DocumentSequence
        {
            Id              = Guid.NewGuid(),
            SubscriberId    = subscriberId,
            CompanyId       = companyId,
            EmissionPointId = emissionPointId,
            DocTypeCode     = docTypeCode.Trim(),
            CurrentSeq      = 0,
            UpdatedAt       = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Devuelve el secuencial actual formateado (D9) e incrementa el contador en memoria.
    /// PRECONDICIÓN: la fila debe estar bloqueada con SELECT FOR UPDATE.
    /// </summary>
    public string CaptureAndIncrement()
    {
        var formatted = CurrentSeq.ToString("D9");
        CurrentSeq++;
        UpdatedAt = DateTime.UtcNow;
        return formatted;
    }
}
