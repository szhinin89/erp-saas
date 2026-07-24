using FluentValidation;

namespace ERP.Application.Modules.Branches.UseCases.CreateBranch;

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la sucursal es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("La dirección es obligatoria.")
            .MaximumLength(200).WithMessage("La dirección no puede exceder 200 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(300).WithMessage("La descripción no puede exceder 300 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.Reference)
            .MaximumLength(100).WithMessage("La referencia no puede exceder 100 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Reference));

        RuleFor(x => x.PostalCode)
            .MaximumLength(20).WithMessage("El código postal no puede exceder 20 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.PostalCode));

        RuleFor(x => x.Phone)
            .MaximumLength(200).WithMessage("El teléfono no puede exceder 200 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.SecondaryPhone)
            .MaximumLength(200).WithMessage("El teléfono secundario no puede exceder 200 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.SecondaryPhone));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El correo de la sucursal no es válido.")
            .MaximumLength(150).WithMessage("El correo no puede exceder 150 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Website)
            .MaximumLength(200).WithMessage("El sitio web no puede exceder 200 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Website));

        RuleFor(x => x.ManagerName)
            .MaximumLength(100).WithMessage("El nombre del responsable no puede exceder 100 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.ManagerName));

        RuleFor(x => x.ManagerPosition)
            .MaximumLength(100).WithMessage("El cargo del responsable no puede exceder 100 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.ManagerPosition));

        RuleFor(x => x.ManagerEmail)
            .EmailAddress().WithMessage("El correo del responsable no es válido.")
            .MaximumLength(150).WithMessage("El correo del responsable no puede exceder 150 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.ManagerEmail));

        RuleFor(x => x.ManagerPhone)
            .MaximumLength(200).WithMessage("El teléfono del responsable no puede exceder 200 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.ManagerPhone));

        RuleFor(x => x.Latitude)
            .MaximumLength(25).WithMessage("La latitud no puede exceder 25 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Latitude));

        RuleFor(x => x.Longitude)
            .MaximumLength(25).WithMessage("La longitud no puede exceder 25 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Longitude));

        RuleFor(x => x.InternalNotes)
            .MaximumLength(1000).WithMessage("Las notas internas no pueden exceder 1000 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.InternalNotes));
    }
}
