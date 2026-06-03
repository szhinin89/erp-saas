namespace ERP.Application.Configuration.DTOs;

/// <summary>DTO del perfil de facturación canónico (fusión de BillingSettings + SubscriberBillingProfile).</summary>
public sealed record BillingSettingsDto(
    Guid    Id,
    Guid    SubscriberId,
    // Identificación fiscal SRI
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
    // Régimen fiscal
    bool    RequiresAccounting,
    string? SpecialTaxpayer,
    // Configuración de recibos
    string? LogoBase64,
    string? FooterText,
    int     ReceiptWidth,
    // Vínculo SaaS
    Guid?   BusinessPartnerId);
