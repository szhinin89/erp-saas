using FluentValidation;

namespace ERP.Application.Modules.Branches.UseCases.EnableBranch;

public sealed class EnableBranchCommandValidator : AbstractValidator<EnableBranchCommand>
{
    public EnableBranchCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID de la sucursal es obligatorio.");
    }
}
