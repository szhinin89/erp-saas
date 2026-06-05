using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Configuration.UseCases.UpsertSubscriberBillingProfile;

public sealed record UpsertSubscriberBillingProfileCommand(
    string  IdentificationType,
    string  IdentificationNumber,
    string  LegalName,
    string? TradeName,
    string  Address,
    string? Phone,
    string? Email,
    string  Country,
    string? City,
    bool    RequiresAccounting,
    string? SpecialTaxpayer,
    string? LogoBase64,
    string? FooterText,
    int     ReceiptWidth,
    Guid?   BusinessPartnerId)
    : IRequest<Result<ERP.Application.Configuration.DTOs.SubscriberBillingProfileDto>>;