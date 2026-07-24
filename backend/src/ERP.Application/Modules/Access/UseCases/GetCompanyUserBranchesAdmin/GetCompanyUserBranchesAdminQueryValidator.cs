using FluentValidation;

namespace ERP.Application.Access.UseCases.GetCompanyUserBranchesAdmin;

public sealed class GetCompanyUserBranchesAdminQueryValidator : AbstractValidator<GetCompanyUserBranchesAdminQuery>
{
    public GetCompanyUserBranchesAdminQueryValidator()
    {
        RuleFor(x => x.CompanyUserId).NotEmpty()
            .WithMessage("El usuario de empresa es obligatorio.");
    }
}
