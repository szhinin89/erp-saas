using FluentValidation;

namespace ERP.Application.Tenants.UseCases.CreateTenant;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la empresa es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("El slug es obligatorio.")
            .MaximumLength(100).WithMessage("El slug no puede exceder 100 caracteres.");

        RuleFor(x => x.PasswordResetMode)
            .IsInEnum().WithMessage("El modo de recuperación de contraseña no es válido.");

        RuleFor(x => x.Ruc)
            .MaximumLength(15).WithMessage("El RUC no puede exceder 15 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Ruc));

        RuleFor(x => x.ShortName)
            .MaximumLength(100).WithMessage("El nombre corto no puede exceder 100 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.ShortName));

        RuleFor(x => x.TradeName)
            .MaximumLength(120).WithMessage("El nombre comercial no puede exceder 120 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.TradeName));

        RuleFor(x => x.Dinardap)
            .MaximumLength(20).WithMessage("Dinardap no puede exceder 20 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Dinardap));

        RuleFor(x => x.LogoUrl)
            .MaximumLength(500).WithMessage("La URL del logo no puede exceder 500 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.LogoUrl));

        RuleFor(x => x.PlanCode)
            .MaximumLength(64).WithMessage("El código de plan no puede exceder 64 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.PlanCode));

        When(x => x.EnabledModules is { Count: > 0 }, () =>
        {
            RuleForEach(x => x.EnabledModules!)
                .NotEmpty().WithMessage("Cada clave de módulo debe ser no vacía.")
                .MaximumLength(64).WithMessage("Cada clave de módulo no puede exceder 64 caracteres.");
        });
    }
}
