using FluentValidation;

namespace ERP.Application.Modules.Branches.UseCases.CreateBranch;

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la sucursal es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("La dirección es obligatoria.")
            .MaximumLength(400).WithMessage("La dirección no puede exceder 400 caracteres.");

        RuleFor(x => x.Reference)
            .MaximumLength(400).WithMessage("La referencia no puede exceder 400 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Reference));

        RuleFor(x => x.Phones)
            .MaximumLength(120).WithMessage("Los teléfonos no pueden exceder 120 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phones));

        RuleFor(x => x.Latitude)
            .MaximumLength(50).WithMessage("La latitud no puede exceder 50 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Latitude));

        RuleFor(x => x.Longitude)
            .MaximumLength(50).WithMessage("La longitud no puede exceder 50 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Longitude));

        RuleFor(x => x.RechargeOption)
            .MaximumLength(50).WithMessage("La opción de recarga no puede exceder 50 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.RechargeOption));
    }
}
