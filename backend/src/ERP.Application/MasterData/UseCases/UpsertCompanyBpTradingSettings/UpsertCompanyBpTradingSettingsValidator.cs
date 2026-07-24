using ERP.Domain.MasterData.Entities;
using FluentValidation;

namespace ERP.Application.MasterData.UseCases.UpsertCompanyBpTradingSettings;

public sealed class UpsertCompanyBpTradingSettingsValidator
    : AbstractValidator<UpsertCompanyBpTradingSettingsCommand>
{
    public UpsertCompanyBpTradingSettingsValidator()
    {
        RuleFor(x => x.BusinessPartnerId).NotEmpty();

        RuleFor(x => x.CreditLimit)
            .GreaterThanOrEqualTo(0).WithMessage("El límite de crédito no puede ser negativo.");

        RuleFor(x => x.PaymentDays)
            .GreaterThanOrEqualTo(0).WithMessage("El plazo de pago no puede ser negativo.");

        RuleFor(x => x.CreditCurrencyCode)
            .NotEmpty()
            .Length(CompanyBpTradingSettings.CurrencyCodeLen)
            .WithMessage($"El código de moneda debe ser un ISO 4217 de {CompanyBpTradingSettings.CurrencyCodeLen} letras.");
    }
}
