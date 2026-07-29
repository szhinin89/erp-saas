using FluentValidation;

namespace ERP.Application.Access.UseCases.CloseUserSessionAdmin;

public sealed class CloseUserSessionAdminValidator : AbstractValidator<CloseUserSessionAdminCommand>
{
    public CloseUserSessionAdminValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("El identificador de la sesión es obligatorio.");
    }
}
