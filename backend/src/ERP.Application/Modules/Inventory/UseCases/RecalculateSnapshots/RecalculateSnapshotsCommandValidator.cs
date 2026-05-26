using FluentValidation;

namespace ERP.Application.Inventory.UseCases.RecalculateSnapshots;

public sealed class RecalculateSnapshotsCommandValidator : AbstractValidator<RecalculateSnapshotsCommand>
{
    public RecalculateSnapshotsCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Si se indica producto, el GUID no puede ser vacío.");

        RuleFor(x => x.WarehouseId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Si se indica Warehouse, el GUID no puede ser vacío.");

        RuleFor(x => x.DateTo)
            .Must(h => h is null || h.Value.Date <= DateTime.UtcNow.Date)
            .WithMessage("La fecha 'Hasta' no puede ser posterior a hoy (UTC).");
    }
}
