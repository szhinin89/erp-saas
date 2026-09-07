using ERP.Domain.Modules.SriCatalogs.Interfaces;
using FluentValidation;

namespace ERP.Application.MasterData.UseCases.AddSupplierRetentionDefault;

public sealed class AddSupplierRetentionDefaultValidator
    : AbstractValidator<AddSupplierRetentionDefaultCommand>
{
    public AddSupplierRetentionDefaultValidator(ISriCatalogLookupRepository catalogRepo)
    {
        RuleFor(x => x.BusinessPartnerId).NotEmpty();
        RuleFor(x => x.SriRetentionCodeId).NotEmpty();

        RuleFor(x => x.SriRetentionCodeId)
            .MustAsync(
                async (id, ct) =>
                {
                    var code = await catalogRepo.GetRetentionCodeByIdAsync(id, ct);
                    return code is { IsActive: true };
                }
            )
            .WithMessage(
                "SriRetentionCodeId no corresponde a un código activo del catálogo sri_retention_code."
            );
    }
}
