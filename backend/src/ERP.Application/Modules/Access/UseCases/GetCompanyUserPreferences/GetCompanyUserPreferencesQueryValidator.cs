using FluentValidation;

namespace ERP.Application.Access.UseCases.GetCompanyUserPreferences;

public sealed class GetCompanyUserPreferencesQueryValidator
    : AbstractValidator<GetCompanyUserPreferencesQuery>
{
    public GetCompanyUserPreferencesQueryValidator()
    {
        RuleFor(x => x.CompanyUserMembershipId)
            .NotEmpty()
            .WithMessage("La membresía es obligatoria.");
    }
}
