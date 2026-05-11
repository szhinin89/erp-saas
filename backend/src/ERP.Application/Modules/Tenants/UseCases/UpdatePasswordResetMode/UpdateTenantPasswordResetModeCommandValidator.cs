using FluentValidation;

namespace ERP.Application.Tenants.UseCases.UpdatePasswordResetMode;

public sealed class UpdateTenantPasswordResetModeCommandValidator : AbstractValidator<UpdateTenantPasswordResetModeCommand>
{
    public UpdateTenantPasswordResetModeCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");

        RuleFor(x => x.PasswordResetMode)
            .IsInEnum().WithMessage("El modo de recuperación de contraseña no es válido.");
    }
}
