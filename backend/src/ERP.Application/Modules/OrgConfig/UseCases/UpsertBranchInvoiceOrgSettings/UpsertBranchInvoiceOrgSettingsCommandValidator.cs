using FluentValidation;

namespace ERP.Application.Modules.OrgConfig.UseCases.UpsertBranchInvoiceOrgSettings;

public sealed class UpsertBranchInvoiceOrgSettingsCommandValidator
    : AbstractValidator<UpsertBranchInvoiceOrgSettingsCommand>
{
    public UpsertBranchInvoiceOrgSettingsCommandValidator()
    {
        RuleFor(c => c.BranchId)
            .NotEmpty()
            .WithMessage("La sucursal es requerida.");
    }
}
