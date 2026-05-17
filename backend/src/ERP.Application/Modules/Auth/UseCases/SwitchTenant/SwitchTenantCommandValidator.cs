using FluentValidation;

namespace ERP.Application.Auth.UseCases.SwitchTenant;

public sealed class SwitchTenantCommandValidator : AbstractValidator<SwitchTenantCommand>
{
    public SwitchTenantCommandValidator()
    {
        // Guid.Empty is valid: SuperAdmin sends it to return to the global panel.
        // Business validation (tenant exists, user is SuperAdmin) is handled in the handler.
    }
}
