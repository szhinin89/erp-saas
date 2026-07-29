using FluentValidation;

namespace ERP.Application.Access.UseCases.UpdateCompanyUserBranchesAdmin;

public sealed class UpdateCompanyUserBranchesAdminCommandValidator
    : AbstractValidator<UpdateCompanyUserBranchesAdminCommand>
{
    public UpdateCompanyUserBranchesAdminCommandValidator()
    {
        RuleFor(x => x.CompanyUserMembershipId)
            .NotEmpty()
            .WithMessage("El usuario de empresa es obligatorio.");

        RuleFor(x => x.AuthorizedBranchIds)
            .NotNull()
            .WithMessage("La lista de sucursales autorizadas es obligatoria (puede estar vacía).");

        RuleForEach(x => x.AuthorizedBranchIds)
            .NotEqual(Guid.Empty)
            .WithMessage("La sucursal a autorizar no puede ser un Guid vacío.");
    }
}
