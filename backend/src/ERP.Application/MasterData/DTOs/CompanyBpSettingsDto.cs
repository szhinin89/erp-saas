using ERP.Domain.MasterData.Entities;

namespace ERP.Application.MasterData.DTOs;

public sealed record CompanyBpSettingsDto(
    Guid     BusinessPartnerId,
    decimal? CreditLimit,
    short    PaymentDays,
    bool     IsBlocked,
    string?  CreditCurrencyCode)
{
    public static CompanyBpSettingsDto From(CompanyBusinessPartnerSettings s) => new(
        s.BusinessPartnerId,
        s.CreditLimit,
        s.PaymentDays,
        s.IsBlocked,
        s.CreditCurrencyCode);
}
