using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
using FluentValidation;

namespace ERP.Application.MasterData.UseCases.CreateBusinessPartner;

public sealed class CreateBusinessPartnerValidator : AbstractValidator<CreateBusinessPartnerCommand>
{
    private static readonly string[] ValidSriTypes =
        [TaxIdentification.SriRuc, TaxIdentification.SriCi, TaxIdentification.SriPassport,
         TaxIdentification.SriConsumidorFinal, TaxIdentification.SriExterior, TaxIdentification.SriPlaca];

    public CreateBusinessPartnerValidator()
    {
        RuleFor(x => x.IdentificationType)
            .NotEmpty().WithMessage("El tipo de identificación es obligatorio.")
            .Must(t => ValidSriTypes.Contains(t))
            .WithMessage("Tipo de identificación inválido. Use: 04=RUC, 05=CI, 06=Pasaporte, 07=ConsumidorFinal, 08=Exterior, 09=Placa.");

        RuleFor(x => x.IdentificationNumber)
            .NotEmpty().WithMessage("El número de identificación es obligatorio.")
            .MaximumLength(TaxIdentification.NumberMaxLen)
            .WithMessage($"El número no puede superar {TaxIdentification.NumberMaxLen} caracteres.");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("El nombre legal es obligatorio.")
            .MinimumLength(2).WithMessage("El nombre legal debe tener al menos 2 caracteres.")
            .MaximumLength(PersonName.LegalNameMaxLen)
            .WithMessage($"El nombre legal no puede superar {PersonName.LegalNameMaxLen} caracteres.");

        RuleFor(x => x.TradeName)
            .MaximumLength(PersonName.TradeNameMaxLen)
            .WithMessage($"El nombre comercial no puede superar {PersonName.TradeNameMaxLen} caracteres.")
            .When(x => x.TradeName is not null);

        RuleFor(x => x.PersonType)
            .IsInEnum().WithMessage("Tipo de persona inválido.");

        RuleFor(x => x.CountryCode)
            .Length(2).WithMessage("CountryCode debe ser un código ISO 3166-1 alpha-2 de 2 caracteres.")
            .When(x => x.CountryCode is not null);
    }
}
