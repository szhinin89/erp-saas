using ERP.Application.Auth;
using FluentValidation;

namespace ERP.Application.Setup.CreateInitialAdmin;

public sealed class CreateInitialAdminCommandValidator : AbstractValidator<CreateInitialAdminCommand>
{
    public CreateInitialAdminCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .Matches("^[a-zA-Z0-9][a-zA-Z0-9._-]{1,48}[a-zA-Z0-9]$")
            .WithMessage("El username debe tener 3-50 caracteres: letras, números, punto, guion o guion bajo.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Password).NotEmpty().ApplyPasswordComplexity();
        RuleFor(x => x.SetupToken).NotEmpty();
    }
}
