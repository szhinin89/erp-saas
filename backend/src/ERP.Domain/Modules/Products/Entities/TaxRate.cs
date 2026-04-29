using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>
/// Catálogo de tarifas de impuestos.
/// Usado para: IVA venta, IVA compra, ICE.
/// El porcentaje se almacena como decimal (ej: 15.00 para 15%).
/// </summary>
public class TaxRate : MasterEntity
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public TaxRateType Type { get; private set; }
    public decimal Percentage { get; private set; }

    private TaxRate() { }

    public static TaxRate Create(
        Guid tenantId,
        string code,
        string name,
        TaxRateType type,
        decimal percentage,
        Guid createdBy)
    {
        if (percentage < 0 || percentage > 100)
            throw new ArgumentException("El porcentaje debe estar entre 0 y 100.", nameof(percentage));

        var tax = new TaxRate
        {
            Id         = Guid.NewGuid(),
            TenantId   = tenantId,
            Code       = code,
            Name       = name,
            Type       = type,
            Percentage = percentage,
        };
        tax.SetCreated(createdBy);
        return tax;
    }

    public void Update(string name, decimal percentage, Guid updatedBy)
    {
        if (percentage < 0 || percentage > 100)
            throw new ArgumentException("El porcentaje debe estar entre 0 y 100.", nameof(percentage));

        Name       = name;
        Percentage = percentage;
        SetUpdated(updatedBy);
    }
}

public enum TaxRateType
{
    VAT    = 1,   // IVA (Impuesto al Valor Agregado)
    Excise = 2,   // ICE (Impuesto a los Consumos Especiales)
    Other  = 99
}
