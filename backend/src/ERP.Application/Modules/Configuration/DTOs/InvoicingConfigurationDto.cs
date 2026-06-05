namespace ERP.Application.Configuration.DTOs;

/// <summary>DTO del perfil de facturaciÃ³n canÃ³nico (fusiÃ³n de BillingSettings + SubscriberBillingProfile).</summary>
public sealed record SubscriberBillingProfileDto(
    Guid    Id,
    Guid    SubscriberId,
    // IdentificaciÃ³n fiscal SRI
    string  IdentificationType,
    string  IdentificationNumber,
    // Datos legales
    string  LegalName,
    string? TradeName,
    string  Address,
    string? Phone,
    string? Email,
    string  Country,
    string? City,
    // RÃ©gimen fiscal
    bool    RequiresAccounting,
    string? SpecialTaxpayer,
    // ConfiguraciÃ³n de recibos
    string? LogoBase64,
    string? FooterText,
    int     ReceiptWidth,
    // VÃ­nculo SaaS
    Guid?   BusinessPartnerId);
