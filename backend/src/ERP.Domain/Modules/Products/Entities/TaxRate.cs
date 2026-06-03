using ERP.Domain.Common;
using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Products.Entities;

/// <summary>
/// Catálogo de tarifas de impuestos por suscriptor.
/// El porcentaje NO se almacena aquí — se obtiene de la fuente global:
///   VAT    → global.sri_vat_rate (SriVatRate.Code)
///   Excise → global.sri_ice_rate (SriIceRate.Code)
/// </summary>
public class TaxRate : MasterEntity
{
    public string      Code       { get; private set; } = null!;
    public string      Name       { get; private set; } = null!;
    public TaxRateType Type       { get; private set; }

    /// <summary>FK a global.sri_vat_rate.Code (si Type == VAT).</summary>
    public string? SriVatCode  { get; private set; }
    /// <summary>FK a global.sri_ice_rate.Code (si Type == Excise).</summary>
    public string? SriIceCode  { get; private set; }

    /// <summary>Navigation — fuente del porcentaje para tipo VAT.</summary>
    public SriVatRate? SriVatRate { get; private set; }
    /// <summary>Navigation — fuente del porcentaje para tipo Excise.</summary>
    public SriIceRate? SriIceRate { get; private set; }

    /// <summary>Porcentaje desde la fuente global canónica. 0 si navigation no cargada.</summary>
    public decimal Percentage =>
        Type == TaxRateType.VAT    ? (SriVatRate?.Percentage ?? 0m) :
        Type == TaxRateType.Excise ? (SriIceRate?.Percentage ?? 0m) : 0m;

    private TaxRate() { }

    public static TaxRate Create(
        Guid        subscriberId,
        string      code,
        string      name,
        TaxRateType type,
        string?     sriVatCode,
        string?     sriIceCode,
        Guid        createdBy)
    {
        if (type == TaxRateType.VAT && string.IsNullOrWhiteSpace(sriVatCode))
            throw new ArgumentException("sriVatCode es requerido para tipo VAT.", nameof(sriVatCode));
        if (type == TaxRateType.Excise && string.IsNullOrWhiteSpace(sriIceCode))
            throw new ArgumentException("sriIceCode es requerido para tipo Excise.", nameof(sriIceCode));

        var tax = new TaxRate
        {
            Id           = Guid.NewGuid(),
            SubscriberId = subscriberId,
            Code         = code,
            Name         = name,
            Type         = type,
            SriVatCode   = sriVatCode?.Trim(),
            SriIceCode   = sriIceCode?.Trim(),
        };
        tax.SetCreated(createdBy);
        return tax;
    }

    public void Update(string name, Guid updatedBy)
    {
        Name = name;
        SetUpdated(updatedBy);
    }
}

public enum TaxRateType
{
    VAT    = 1,
    Excise = 2,
    Other  = 99
}
