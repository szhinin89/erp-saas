using FluentValidation;

namespace ERP.Application.Modules.Branches.UseCases.DisableBranch;

public sealed class DisableBranchCommandValidator : AbstractValidator<DisableBranchCommand>
{
    public DisableBranchCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("El ID de la sucursal es obligatorio.");
    }
}
