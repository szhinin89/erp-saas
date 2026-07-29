using ERP.Domain.Common;
using static ERP.Domain.Common.FiscalPrecision;

namespace ERP.Domain.Modules.Purchases.Entities;

public sealed class IssuedWithholdingDetail : IMustHaveTenant
{
    public const int TaxTypeMaxLen = 10;
    public const int RetentionCodeMaxLen = 10;
    public const int CodeDescMaxLen = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WithholdingId { get; private set; }
    public string TaxType { get; private set; } = null!;
    public string RetentionCode { get; private set; } = null!;
    public string RetentionCodeDescription { get; private set; } = null!;
    public decimal TaxableBase { get; private set; }
    public decimal RetentionPct { get; private set; }
    public decimal AmountRetained { get; private set; }

    private IssuedWithholdingDetail() { }

    public static IssuedWithholdingDetail Create(
        Guid withholdingId,
        Guid tenantId,
        string taxType,
        string retentionCode,
        string retentionCodeDescription,
        decimal taxableBase,
        decimal retentionPct
    )
    {
        if (string.IsNullOrWhiteSpace(taxType))
            throw new ArgumentException("El tipo de impuesto es obligatorio.", nameof(taxType));
        if (string.IsNullOrWhiteSpace(retentionCode))
            throw new ArgumentException(
                "El código de retención es obligatorio.",
                nameof(retentionCode)
            );
        if (taxableBase < 0)
            throw new ArgumentException(
                "La base imponible no puede ser negativa.",
                nameof(taxableBase)
            );
        if (retentionPct < 0)
            throw new ArgumentException(
                "El porcentaje de retención no puede ser negativo.",
                nameof(retentionPct)
            );

        return new IssuedWithholdingDetail
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WithholdingId = withholdingId,
            TaxType = taxType.Trim().ToUpperInvariant(),
            RetentionCode = retentionCode.Trim(),
            RetentionCodeDescription = retentionCodeDescription.Trim(),
            TaxableBase = taxableBase,
            RetentionPct = retentionPct,
            AmountRetained = Math.Round(
                taxableBase * retentionPct / 100m,
                TaxAmount,
                MidpointRounding.AwayFromZero
            ),
        };
    }
}
