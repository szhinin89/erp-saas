using ERP.Domain.Products.Entities;
using FluentValidation;

namespace ERP.Application.Products.UseCases.CreateTaxRate;

public sealed class CreateTaxRateCommandValidator : AbstractValidator<CreateTaxRateCommand>
{
    public CreateTaxRateCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código de tarifa es obligatorio.")
            .MaximumLength(20).WithMessage("El código no puede exceder 20 caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de tarifa es obligatorio.")
            .MaximumLength(120).WithMessage("El nombre no puede exceder 120 caracteres.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("El tipo de tarifa no es válido.");

        RuleFor(x => x.SriVatCode)
            .NotEmpty().WithMessage("sriVatCode es requerido para tipo VAT.")
            .When(x => x.Type == TaxRateType.VAT);

        RuleFor(x => x.SriIceCode)
            .NotEmpty().WithMessage("sriIceCode es requerido para tipo Excise.")
            .When(x => x.Type == TaxRateType.Excise);
    }
}
