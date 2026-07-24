using FluentValidation;

namespace ERP.Application.Access.UseCases.LookupUserByUsernameAdmin;

public sealed class LookupUserByUsernameAdminQueryValidator : AbstractValidator<LookupUserByUsernameAdminQuery>
{
    public LookupUserByUsernameAdminQueryValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
    }
}
