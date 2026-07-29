using ERP.Domain.Access.Entities;
using FluentValidation;

namespace ERP.Application.Access.UseCases.CreateAuthenticatedSession;

public sealed class CreateAuthenticatedSessionValidator
    : AbstractValidator<CreateAuthenticatedSessionCommand>
{
    public CreateAuthenticatedSessionValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("El tenant es obligatorio.");
        RuleFor(x => x.CompanyId).NotEmpty().WithMessage("La empresa es obligatoria.");
        RuleFor(x => x.IdentityUserId).NotEmpty().WithMessage("El usuario es obligatorio.");
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("La sucursal es obligatoria.");
        RuleFor(x => x.TerminalId)
            .NotEmpty()
            .WithMessage("El terminal es obligatorio.")
            .MaximumLength(UserSession.TerminalIdMaxLen)
            .WithMessage(
                $"El terminal no puede exceder {UserSession.TerminalIdMaxLen} caracteres."
            );
    }
}
