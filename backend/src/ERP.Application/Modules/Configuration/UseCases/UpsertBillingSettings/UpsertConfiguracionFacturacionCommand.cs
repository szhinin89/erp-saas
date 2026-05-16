using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Configuration.UseCases.UpsertBillingSettings;

public sealed record UpsertBillingSettingsCommand(
    string  LegalName,
    string  TradeName,
    string  Ruc,
    string  MainAddress,
    string  Phone,
    string? Email,
    bool    RequiresAccounting,
    string? SpecialTaxpayer,
    string? LogoBase64,
    string? FooterText,
    int     ReceiptWidth)
    : IRequest<Result<ERP.Application.Configuration.DTOs.BillingSettingsDto>>;
