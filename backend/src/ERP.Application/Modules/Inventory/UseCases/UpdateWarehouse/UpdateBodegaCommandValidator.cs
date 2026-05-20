using FluentValidation;
using ERP.Application.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.ActualizarBodega;

public sealed class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator(
        IWarehouseRepository repo,
        ICurrentSubscriber tenant)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID de la Warehouse es obligatorio.");

        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("La sucursal es obligatoria.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la Warehouse es obligatorio.")
            .MaximumLength(Warehouse.NameMaxLen)
            .WithMessage($"El nombre no puede exceder {Warehouse.NameMaxLen} caracteres.")
            .MustAsync(async (command, nombre, ct) =>
                !await repo.ExistsNameAsync(tenant.SubscriberId, nombre, command.Id, ct))
            .WithMessage("Ya existe otra Warehouse con ese nombre en el tenant.");

        RuleFor(x => x.Address)
            .MaximumLength(Warehouse.AddressMaxLen)
            .WithMessage($"La ubicación no puede exceder {Warehouse.AddressMaxLen} caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Address));

        RuleFor(x => x.Manager)
            .MaximumLength(Warehouse.ManagerMaxLen)
            .WithMessage($"El encargado no puede exceder {Warehouse.ManagerMaxLen} caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Manager));
    }
}
