using ERP.Domain.Common;
using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Domain.Modules.Retentions.Entities;

/// <summary>
/// Línea hija de <c>RetentionDocument</c> — un rubro retenido (IVA o Renta) sobre el documento
/// origen. Estructura calcada de <c>IssuedWithholdingDetail</c> (ya validada en producción para
/// Compras, ver <c>docs/decisions/RETENTIONS-MODULE-DESIGN-01.md</c>), con <c>TaxType</c> como
/// enum (<see cref="RetentionTaxType"/>) en vez de <c>string</c> porque este módulo es nuevo y no
/// arrastra la representación textual heredada de <c>IssuedWithholdingDetail.TaxType</c>.
///
/// No hardcodea ningún código/porcentaje SRI — <c>RetentionCode</c>/<c>RetentionRate</c> llegan
/// siempre como parámetros desde quien construye la línea (fase de integración futura vía
/// <c>IRetentionCodeResolver</c>, fuera de alcance de esta fase).
/// </summary>
public sealed class RetentionDocumentLine : IMustHaveTenant
{
    public const int RetentionCodeMaxLen = 10;
    public const int DescriptionMaxLen = 300;
    public const int RetentionCodeDescriptionMaxLen = 300;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RetentionDocumentId { get; private set; }
    public RetentionTaxType TaxType { get; private set; }
    public string RetentionCode { get; private set; } = null!;

    /// <summary>
    /// Snapshot REQUERIDO del texto del código de retención (<c>SriRetentionCode.Name</c>) en el
    /// momento de emitir la línea — nunca se resuelve dinámicamente contra el catálogo después,
    /// para que una retención antigua pueda reconstruir el texto exacto que tenía al emitirse
    /// aunque el catálogo cambie más adelante (requisito legal de reproducibilidad, ver
    /// <c>IssuedWithholdingDetail.RetentionCodeDescription</c>, mismo patrón ya usado en Compras).
    /// Distinto de <see cref="Description"/> (nota libre opcional del usuario) — ambos coexisten.
    /// </summary>
    public string RetentionCodeDescription { get; private set; } = null!;
    public decimal BaseAmount { get; private set; }
    public decimal RetentionRate { get; private set; }
    public decimal RetainedAmount { get; private set; }
    public string? Description { get; private set; }

    private RetentionDocumentLine() { }

    public static RetentionDocumentLine Create(
        Guid retentionDocumentId,
        Guid tenantId,
        RetentionTaxType taxType,
        string retentionCode,
        string retentionCodeDescription,
        decimal baseAmount,
        decimal retentionRate,
        decimal retainedAmount,
        string? description = null
    )
    {
        if (retentionDocumentId == Guid.Empty)
            throw new ArgumentException(
                "El documento de retención es obligatorio.",
                nameof(retentionDocumentId)
            );
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));
        if (!Enum.IsDefined(taxType))
            throw new ArgumentException("El tipo de impuesto retenido no es válido.", nameof(taxType));
        if (string.IsNullOrWhiteSpace(retentionCode))
            throw new ArgumentException(
                "El código de retención es obligatorio.",
                nameof(retentionCode)
            );
        if (string.IsNullOrWhiteSpace(retentionCodeDescription))
            throw new ArgumentException(
                "La descripción del código de retención es obligatoria.",
                nameof(retentionCodeDescription)
            );
        if (baseAmount <= 0)
            throw new ArgumentException(
                "La base imponible debe ser mayor a cero.",
                nameof(baseAmount)
            );
        if (retentionRate <= 0)
            throw new ArgumentException(
                "El porcentaje de retención debe ser mayor a cero.",
                nameof(retentionRate)
            );
        if (retainedAmount <= 0)
            throw new ArgumentException(
                "El monto retenido debe ser mayor a cero.",
                nameof(retainedAmount)
            );
        if (retainedAmount > baseAmount)
            throw new ArgumentException(
                "El monto retenido no puede ser mayor a la base imponible.",
                nameof(retainedAmount)
            );

        return new RetentionDocumentLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RetentionDocumentId = retentionDocumentId,
            TaxType = taxType,
            RetentionCode = retentionCode.Trim(),
            RetentionCodeDescription = retentionCodeDescription.Trim(),
            BaseAmount = Math.Round(baseAmount, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero),
            RetentionRate = Math.Round(retentionRate, FiscalPrecision.Percentage, MidpointRounding.AwayFromZero),
            RetainedAmount = Math.Round(retainedAmount, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero),
            Description = Normalize(description),
        };
    }

    private static string? Normalize(string? value) => value?.Trim() is { Length: > 0 } text ? text : null;
}
