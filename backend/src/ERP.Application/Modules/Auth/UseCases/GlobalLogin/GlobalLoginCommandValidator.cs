using FluentValidation;

namespace ERP.Application.Auth.UseCases.GlobalLogin;

public sealed class GlobalLoginCommandValidator : AbstractValidator<GlobalLoginCommand>
{
    public GlobalLoginCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(200);
    }
}
