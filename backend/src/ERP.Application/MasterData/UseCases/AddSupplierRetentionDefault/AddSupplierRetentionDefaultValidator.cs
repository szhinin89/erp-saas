using ERP.Application.Modules.Purchases.Services;
using FluentValidation;

namespace ERP.Application.MasterData.UseCases.AddSupplierRetentionDefault;

public sealed class AddSupplierRetentionDefaultValidator
    : AbstractValidator<AddSupplierRetentionDefaultCommand>
{
    /// <summary>
    /// Un default nuevo es una configuración para operaciones NUEVAS: solo se aceptan conceptos
    /// seleccionables según la lectura oficial del catálogo (<see cref="IRetentionCodeResolver.GetSelectableByIdAsync"/>,
    /// ADR-037 D13) — sin filtros de habilitación propios. Los defaults existentes que apunten a un concepto
    /// ya no habilitado (p. ej. 728) siguen legibles por sus handlers de lectura.
    /// </summary>
    public AddSupplierRetentionDefaultValidator(IRetentionCodeResolver retentionCodeResolver)
    {
        RuleFor(x => x.BusinessPartnerId).NotEmpty();
        RuleFor(x => x.SriRetentionCodeId).NotEmpty();

        RuleFor(x => x.SriRetentionCodeId)
            .MustAsync(
                async (id, ct) =>
                    await retentionCodeResolver.GetSelectableByIdAsync(id, ct) is not null
            )
            .WithMessage(
                "SriRetentionCodeId no corresponde a un código activo del catálogo sri_retention_code."
            );
    }
}
