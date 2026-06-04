using ERP.Domain.MasterData.Entities;

namespace ERP.Application.MasterData.DTOs;

public sealed record CompanyBpTradingSettingsDto(
    Guid      Id,
    Guid      BusinessPartnerId,
    decimal   CreditLimit,
    string    CreditCurrencyCode,
    int       PaymentDays,
    bool      IsBlocked,
    string?   BlockedReason,
    DateTime? BlockedAt)
{
    public static CompanyBpTradingSettingsDto From(CompanyBpTradingSettings s) => new(
        s.Id,
        s.BusinessPartnerId,
        s.CreditLimit,
        s.CreditCurrencyCode,
        s.PaymentDays,
        s.IsBlocked,
        s.BlockedReason,
        s.BlockedAt);
}
