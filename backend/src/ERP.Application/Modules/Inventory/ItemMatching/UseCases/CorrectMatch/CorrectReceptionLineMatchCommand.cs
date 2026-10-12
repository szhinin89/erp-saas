using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.ResolveLines;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Inventory.ItemMatching.UseCases.CorrectMatch;

/// <summary>
/// "Corregir relación": la línea XML ya vinculada a un producto (B) pasa a otro producto o
/// presentación (C). En una sola transacción corrige la línea, reemplaza la equivalencia
/// Proveedor + Código (la anterior se deshabilita, nunca se borra) y re-vincula las demás líneas
/// del mismo documento con ese código. Nunca toca una compra ya confirmada.
/// </summary>
public sealed record CorrectReceptionLineMatchCommand(
    Guid PurchaseReceptionLineId,
    Guid ItemId,
    Guid? PackagingLevelId
) : IRequest<Result<CorrectReceptionLineMatchResultDto>>, IBranchScopedRequest;

/// <summary>Líneas del documento cuyo producto/presentación cambió — el formulario las aplica.</summary>
public sealed record CorrectReceptionLineMatchResultDto(
    IReadOnlyList<ResolvedReceptionLineDto> Lines,
    bool EquivalenceUpdated
);

public sealed class CorrectReceptionLineMatchCommandValidator
    : AbstractValidator<CorrectReceptionLineMatchCommand>
{
    public CorrectReceptionLineMatchCommandValidator()
    {
        RuleFor(x => x.PurchaseReceptionLineId)
            .NotEmpty()
            .WithMessage("La línea de recepción es obligatoria.");
        RuleFor(x => x.ItemId).NotEmpty().WithMessage("Seleccione el producto correcto.");
    }
}
