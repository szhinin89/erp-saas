using FluentValidation;

namespace ERP.Application.Access.UseCases.SwitchTenant;

public sealed class SwitchTenantCommandValidator : AbstractValidator<SwitchTenantCommand>
{
    public SwitchTenantCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");
    }
}
