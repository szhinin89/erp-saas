using ERP.Domain.Common;

namespace ERP.Domain.Configuration.Entities;

public sealed class BillingSettings : AuditableEntity, ITenantEntity
{
    public const int LegalNameMaxLen       = 200;
    public const int TradeNameMaxLen       = 200;
    public const int RucMaxLen             = 13;
    public const int AddressMaxLen         = 500;
    public const int PhoneMaxLen           = 50;
    public const int EmailMaxLen           = 200;
    public const int SpecialTaxpayerMaxLen = 200;
    public const int LogoBase64MaxLen      = 10000;
    public const int FooterTextMaxLen      = 500;

    public string  LegalName           { get; private set; } = null!;
    public string  TradeName           { get; private set; } = null!;
    public string  Ruc                 { get; private set; } = null!;
    public string  MainAddress         { get; private set; } = null!;
    public string  Phone               { get; private set; } = null!;
    public string? Email               { get; private set; }
    public bool    RequiresAccounting  { get; private set; }
    public string? SpecialTaxpayer     { get; private set; }
    public string? LogoBase64          { get; private set; }
    public string? FooterText          { get; private set; }
    public int     ReceiptWidth        { get; private set; } = 80;

    private BillingSettings() { }

    public static BillingSettings Create(
        Guid     tenantId,
        string   legalName,
        string   tradeName,
        string   ruc,
        string   mainAddress,
        string   phone,
        string?  email,
        bool     requiresAccounting,
        string?  specialTaxpayer,
        string?  logoBase64,
        string?  footerText,
        int      receiptWidth,
        Guid     createdBy)
    {
        var config = new BillingSettings
        {
            TenantId          = tenantId,
            LegalName         = legalName.Trim(),
            TradeName         = tradeName.Trim(),
            Ruc               = ruc.Trim(),
            MainAddress       = mainAddress.Trim(),
            Phone             = phone.Trim(),
            Email             = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            RequiresAccounting = requiresAccounting,
            SpecialTaxpayer   = string.IsNullOrWhiteSpace(specialTaxpayer) ? null : specialTaxpayer.Trim(),
            LogoBase64        = string.IsNullOrWhiteSpace(logoBase64) ? null : logoBase64.Trim(),
            FooterText        = string.IsNullOrWhiteSpace(footerText) ? null : footerText.Trim(),
            ReceiptWidth      = receiptWidth == 58 ? 58 : 80,
        };
        config.SetCreated(createdBy);
        return config;
    }

    public static BillingSettings CreateDefault(Guid tenantId, Guid createdBy)
        => Create(tenantId, "DEMO COMPANY", "DEMO COMPANY", "9999999999999",
                  "Unknown address", "", null, false, null, null,
                  "Thank you for your purchase.", 80, createdBy);

    public void Update(
        string   legalName,
        string   tradeName,
        string   ruc,
        string   mainAddress,
        string   phone,
        string?  email,
        bool     requiresAccounting,
        string?  specialTaxpayer,
        string?  logoBase64,
        string?  footerText,
        int      receiptWidth,
        Guid     updatedBy)
    {
        LegalName         = legalName.Trim();
        TradeName         = tradeName.Trim();
        Ruc               = ruc.Trim();
        MainAddress       = mainAddress.Trim();
        Phone             = phone.Trim();
        Email             = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        RequiresAccounting = requiresAccounting;
        SpecialTaxpayer   = string.IsNullOrWhiteSpace(specialTaxpayer) ? null : specialTaxpayer.Trim();
        LogoBase64        = string.IsNullOrWhiteSpace(logoBase64) ? null : logoBase64.Trim();
        FooterText        = string.IsNullOrWhiteSpace(footerText) ? null : footerText.Trim();
        ReceiptWidth      = receiptWidth == 58 ? 58 : 80;
        SetUpdated(updatedBy);
    }
}
