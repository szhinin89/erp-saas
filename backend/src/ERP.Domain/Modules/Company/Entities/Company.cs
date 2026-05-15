using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Empresa emisora de comprobantes electrónicos.
/// Cada company representa un RUC único y se asocia 1:1 con un tenant.
/// Todas las tablas operativas de facturación referencian company_id, no tenant_id.
/// </summary>
public class Company
{
    public Guid    Id                 { get; set; }
    public Guid    TenantId           { get; set; }
    public string  Ruc                { get; set; } = null!;
    public string  LegalName          { get; set; } = null!;
    public string? TradeName          { get; set; }
    public string  MainAddress        { get; set; } = null!;
    public string? Phone              { get; set; }
    public string? Email              { get; set; }
    public string? Website            { get; set; }
    public string  CountryCode        { get; set; } = "ECU";
    public string? TaxRegimeCode      { get; set; }
    public bool    IsAccountingReq    { get; set; } = false;
    public string? SpecialTaxpayerNo  { get; set; }
    public bool    IsForeignTrade     { get; set; } = false;
    public bool    WithholdsRenta     { get; set; } = true;
    public bool    WithholdsVat       { get; set; } = true;
    // SRI
    public short   EnvironmentCode    { get; set; } = 2;  // 2=Pruebas por defecto
    public short   EmissionTypeCode   { get; set; } = 1;  // 1=Normal
    public string? WsdlRecvTest       { get; set; }
    public string? WsdlAuthTest       { get; set; }
    public string? WsdlRecvProd       { get; set; }
    public string? WsdlAuthProd       { get; set; }
    // UI
    public string? LogoBase64         { get; set; }
    public string? ExtraLegend        { get; set; }
    public short   ReceiptWidthMm     { get; set; } = 80;
    public bool    IsActive           { get; set; } = true;
    public DateTime CreatedAt         { get; set; }
    public DateTime UpdatedAt         { get; set; }

    // Navigation
    public SriCountry?      Country      { get; set; }
    public SriTaxRegime?    TaxRegime    { get; set; }
    public SriEnvironment?  Environment  { get; set; }
    public SriEmissionType? EmissionType { get; set; }

    public ICollection<DigitalCertificate> Certificates   { get; set; } = [];
    public ICollection<Establishment>      Establishments { get; set; } = [];
    public ICollection<GeneralParameter>   Parameters     { get; set; } = [];
}
