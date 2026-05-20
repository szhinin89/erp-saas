namespace ERP.Application.Configuration.DTOs;

public sealed record BillingSettingsDto(
    Guid Id,
    Guid SubscriberId,
    string LegalName,
    string TradeName,
    string Ruc,
    string MainAddress,
    string Phone,
    string? Email,
    bool RequiresAccounting,
    string? SpecialTaxpayer,
    string? LogoBase64,
    string? AdditionalNote,
    int ReceiptWidth);
