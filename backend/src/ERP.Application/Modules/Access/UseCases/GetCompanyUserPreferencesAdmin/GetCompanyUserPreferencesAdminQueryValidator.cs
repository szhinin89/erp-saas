using FluentValidation;

namespace ERP.Application.Access.UseCases.GetCompanyUserPreferencesAdmin;

public sealed class GetCompanyUserPreferencesAdminQueryValidator
    : AbstractValidator<GetCompanyUserPreferencesAdminQuery>
{
    public GetCompanyUserPreferencesAdminQueryValidator()
    {
        RuleFor(x => x.CompanyUserId)
            .NotEmpty()
            .WithMessage("El usuario de empresa es obligatorio.");
    }
}
