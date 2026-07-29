using ERP.Domain.Common;

namespace ERP.Domain.Modules.Caja.Entities;

public sealed class CashClosingCount : IMustHaveTenant
{
    public const int DenominationLabelMaxLen = 30;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CashSessionId { get; private set; }

    public decimal DenominationValue { get; private set; }
    public string DenominationLabel { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal Total { get; private set; }

    private CashClosingCount() { }

    public static CashClosingCount Create(
        Guid cashSessionId,
        Guid tenantId,
        decimal denominationValue,
        string denominationLabel,
        int quantity
    )
    {
        if (cashSessionId == Guid.Empty)
            throw new ArgumentException("La sesión de caja es obligatoria.", nameof(cashSessionId));
        if (denominationValue <= 0)
            throw new ArgumentException(
                "El valor de la denominación debe ser mayor a cero.",
                nameof(denominationValue)
            );
        if (string.IsNullOrWhiteSpace(denominationLabel))
            throw new ArgumentException(
                "La etiqueta de denominación es obligatoria.",
                nameof(denominationLabel)
            );
        if (quantity < 0)
            throw new ArgumentException("La cantidad no puede ser negativa.", nameof(quantity));

        return new CashClosingCount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashSessionId = cashSessionId,
            DenominationValue = denominationValue,
            DenominationLabel = denominationLabel.Trim(),
            Quantity = quantity,
            Total = denominationValue * quantity,
        };
    }
}
