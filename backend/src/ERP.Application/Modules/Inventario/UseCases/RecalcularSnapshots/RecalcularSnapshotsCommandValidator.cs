using FluentValidation;

namespace ERP.Application.Inventario.UseCases.RecalcularSnapshots;

public sealed class RecalcularSnapshotsCommandValidator : AbstractValidator<RecalcularSnapshotsCommand>
{
    public RecalcularSnapshotsCommandValidator()
    {
        RuleFor(x => x.ProductoId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Si se indica producto, el GUID no puede ser vacío.");

        RuleFor(x => x.BodegaId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Si se indica bodega, el GUID no puede ser vacío.");

        RuleFor(x => x.Hasta)
            .Must(h => h is null || h.Value.Date <= DateTime.UtcNow.Date)
            .WithMessage("La fecha 'Hasta' no puede ser posterior a hoy (UTC).");
    }
}
