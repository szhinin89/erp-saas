using FluentValidation;

namespace ERP.Application.Tenants.UseCases.UpdateTenantGlobalParameters;

public sealed class UpdateTenantGlobalParametersCommandValidator : AbstractValidator<UpdateTenantGlobalParametersCommand>
{
    public UpdateTenantGlobalParametersCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");
    }
}
