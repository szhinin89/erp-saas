using FluentValidation;
using ERP.Application.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Inventory.UseCases.CrearBodega;

public sealed class CreateBodegaCommandValidator : AbstractValidator<CreateBodegaCommand>
{
    public CreateBodegaCommandValidator(
        IWarehouseRepository repo,
        ICurrentTenant tenant)
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("La sucursal es obligatoria.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la Warehouse es obligatorio.")
            .MaximumLength(Warehouse.NameMaxLen)
            .WithMessage($"El nombre no puede exceder {Warehouse.NameMaxLen} caracteres.")
            .MustAsync(async (nombre, ct) =>
                !await repo.ExistsNameAsync(tenant.TenantId, nombre, null, ct))
            .WithMessage("Ya existe una Warehouse con ese nombre en el tenant.");

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
