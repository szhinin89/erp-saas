using ERP.Domain.Common;

namespace ERP.Domain.Configuration.Entities;

public sealed class SriSettings : AuditableEntity, ITenantEntity
{
    public const int RucMaxLen           = 13;
    public const int LegalNameMaxLen     = 200;
    public const int TradeNameMaxLen     = 200;
    public const int AddressMaxLen       = 500;
    public const int SpecialTaxpayerMaxLen = 200;
    public const int EstabMaxLen         = 3;
    public const int EmPointMaxLen       = 3;
    public const int CertPathMaxLen      = 500;
    public const int CertPasswordMaxLen  = 500;
    public const int WsdlUrlMaxLen       = 500;

    public string  Ruc                  { get; private set; } = null!;
    public string  LegalName            { get; private set; } = null!;
    public string? TradeName            { get; private set; }
    public string  MainAddress          { get; private set; } = null!;
    public bool    RequiresAccounting   { get; private set; }
    public string? SpecialTaxpayer      { get; private set; }
    public string  EstabCode            { get; private set; } = "001";
    public string  EmPointCode          { get; private set; } = "001";
    public int     CurrentSequential    { get; private set; } = 1;
    public string  CertP12Path          { get; private set; } = null!;
    public string  CertPassword         { get; private set; } = null!;
    public int     Environment          { get; private set; }
    public int     EmissionType         { get; private set; } = 1;
    public string  WsdlUrl              { get; private set; } = null!;

    private SriSettings() { }

    public static SriSettings Create(
        Guid     tenantId,
        string   ruc,
        string   legalName,
        string?  tradeName,
        string   mainAddress,
        bool     requiresAccounting,
        string?  specialTaxpayer,
        string   estabCode,
        string   emPointCode,
        int      currentSequential,
        string   certP12Path,
        string   certPassword,
        int      environment,
        int      emissionType,
        string   wsdlUrl,
        Guid     createdBy)
    {
        var s = new SriSettings
        {
            TenantId           = tenantId,
            Ruc                = ruc.Trim(),
            LegalName          = legalName.Trim(),
            TradeName          = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim(),
            MainAddress        = mainAddress.Trim(),
            RequiresAccounting = requiresAccounting,
            SpecialTaxpayer    = string.IsNullOrWhiteSpace(specialTaxpayer) ? null : specialTaxpayer.Trim(),
            EstabCode          = string.IsNullOrWhiteSpace(estabCode) ? "001" : estabCode.Trim(),
            EmPointCode        = string.IsNullOrWhiteSpace(emPointCode) ? "001" : emPointCode.Trim(),
            CurrentSequential  = currentSequential <= 0 ? 1 : currentSequential,
            CertP12Path        = certP12Path.Trim(),
            CertPassword       = certPassword.Trim(),
            Environment        = environment,
            EmissionType       = emissionType,
            WsdlUrl            = wsdlUrl.Trim(),
        };
        s.SetCreated(createdBy);
        return s;
    }

    public void Update(
        string   ruc,
        string   legalName,
        string?  tradeName,
        string   mainAddress,
        bool     requiresAccounting,
        string?  specialTaxpayer,
        string   estabCode,
        string   emPointCode,
        string   certP12Path,
        string   certPassword,
        int      environment,
        int      emissionType,
        string   wsdlUrl,
        Guid     updatedBy)
    {
        Ruc                = ruc.Trim();
        LegalName          = legalName.Trim();
        TradeName          = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim();
        MainAddress        = mainAddress.Trim();
        RequiresAccounting = requiresAccounting;
        SpecialTaxpayer    = string.IsNullOrWhiteSpace(specialTaxpayer) ? null : specialTaxpayer.Trim();
        EstabCode          = string.IsNullOrWhiteSpace(estabCode) ? "001" : estabCode.Trim();
        EmPointCode        = string.IsNullOrWhiteSpace(emPointCode) ? "001" : emPointCode.Trim();
        CertP12Path        = certP12Path.Trim();
        CertPassword       = certPassword.Trim();
        Environment        = environment;
        EmissionType       = emissionType;
        WsdlUrl            = wsdlUrl.Trim();
        SetUpdated(updatedBy);
    }

    public void IncrementSequential() => CurrentSequential++;
}
