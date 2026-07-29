using ERP.Domain.MasterData.Entities;

namespace ERP.Application.MasterData.DTOs;

public sealed record CompanyBpTradingSettingsDto(
    Guid Id,
    Guid BusinessPartnerId,
    decimal CreditLimit,
    string CreditCurrencyCode,
    int PaymentDays,
    Guid? PaymentTermId,
    int Installments,
    int DaysBetweenInstallments,
    bool IsBlocked,
    string? BlockedReason,
    DateTime? BlockedAt,
    bool HasCustomConfiguration)
{
    public static CompanyBpTradingSettingsDto From(CompanyBpTradingSettings s) => new(
        s.Id,
        s.BusinessPartnerId,
        s.CreditLimit,
        s.CreditCurrencyCode,
        s.PaymentDays,
        s.PaymentTermId,
        s.Installments,
        s.DaysBetweenInstallments,
        s.IsBlocked,
        s.BlockedReason,
        s.BlockedAt,
        HasCustomConfiguration: true);

    public static CompanyBpTradingSettingsDto Defaults(Guid businessPartnerId) => new(
        Id: Guid.Empty,
        BusinessPartnerId: businessPartnerId,
        CreditLimit: 0,
        CreditCurrencyCode: "USD",
        PaymentDays: 0,
        PaymentTermId: null,
        Installments: 1,
        DaysBetweenInstallments: 0,
        IsBlocked: false,
        BlockedReason: null,
        BlockedAt: null,
        HasCustomConfiguration: false);
}
