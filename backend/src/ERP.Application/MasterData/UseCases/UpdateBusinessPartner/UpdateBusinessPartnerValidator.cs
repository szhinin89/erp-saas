using ERP.Domain.MasterData.ValueObjects;
using FluentValidation;

namespace ERP.Application.MasterData.UseCases.UpdateBusinessPartner;

public sealed class UpdateBusinessPartnerCommandValidator
    : AbstractValidator<UpdateBusinessPartnerCommand>
{
    public UpdateBusinessPartnerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id del BusinessPartner es obligatorio.");

        RuleFor(x => x.LegalName)
            .NotEmpty()
            .WithMessage("El nombre legal es obligatorio.")
            .MinimumLength(2)
            .WithMessage("El nombre legal debe tener al menos 2 caracteres.")
            .MaximumLength(PersonName.LegalNameMaxLen)
            .WithMessage(
                $"El nombre legal no puede superar {PersonName.LegalNameMaxLen} caracteres."
            );

        RuleFor(x => x.TradeName)
            .MaximumLength(PersonName.TradeNameMaxLen)
            .WithMessage(
                $"El nombre comercial no puede superar {PersonName.TradeNameMaxLen} caracteres."
            )
            .When(x => x.TradeName is not null);

        RuleFor(x => x.PersonType).IsInEnum().WithMessage("Tipo de persona inválido.");

        RuleFor(x => x.CountryCode)
            .Length(2)
            .WithMessage("CountryCode debe ser un código ISO 3166-1 alpha-2 de 2 caracteres.")
            .When(x => x.CountryCode is not null);
    }
}

public sealed class UpdateBusinessPartnerIdentificationCommandValidator
    : AbstractValidator<UpdateBusinessPartnerIdentificationCommand>
{
    public UpdateBusinessPartnerIdentificationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.IdentificationType).NotEmpty().MaximumLength(TaxIdentification.TypeMaxLen);
        RuleFor(x => x.IdentificationNumber)
            .NotEmpty()
            .MaximumLength(TaxIdentification.NumberMaxLen);
    }
}
