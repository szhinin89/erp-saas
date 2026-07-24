using FluentValidation;
using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Application.Modules.Inventory.Warehouses.UseCases.CreateWarehouse;

public sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("La sucursal es obligatoria.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la bodega es obligatorio.")
            .MaximumLength(Warehouse.NameMaxLen)
            .WithMessage($"El nombre no puede exceder {Warehouse.NameMaxLen} caracteres.");

        RuleFor(x => x.StorageType)
            .MaximumLength(Warehouse.StorageTypeMaxLen)
            .WithMessage($"El tipo de almacenamiento no puede exceder {Warehouse.StorageTypeMaxLen} caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.StorageType));

        RuleFor(x => x.Address)
            .MaximumLength(Warehouse.AddressMaxLen)
            .WithMessage($"La dirección no puede exceder {Warehouse.AddressMaxLen} caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Address));

        RuleFor(x => x.Phone)
            .MaximumLength(Warehouse.PhoneMaxLen)
            .WithMessage($"El teléfono no puede exceder {Warehouse.PhoneMaxLen} caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El correo no tiene un formato válido.")
            .MaximumLength(Warehouse.EmailMaxLen)
            .WithMessage($"El correo no puede exceder {Warehouse.EmailMaxLen} caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Manager)
            .MaximumLength(Warehouse.ManagerMaxLen)
            .WithMessage($"El responsable no puede exceder {Warehouse.ManagerMaxLen} caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Manager));

        RuleFor(x => x.Latitude)
            .MaximumLength(Warehouse.LatLonMaxLen)
            .WithMessage($"La latitud no puede exceder {Warehouse.LatLonMaxLen} caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Latitude));

        RuleFor(x => x.Longitude)
            .MaximumLength(Warehouse.LatLonMaxLen)
            .WithMessage($"La longitud no puede exceder {Warehouse.LatLonMaxLen} caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Longitude));

        RuleFor(x => x.Capacity)
            .GreaterThanOrEqualTo(0).WithMessage("La capacidad no puede ser negativa.")
            .When(x => x.Capacity.HasValue);

        RuleFor(x => x.DailyDispatchGoal)
            .GreaterThanOrEqualTo(0).WithMessage("La meta de despacho diario no puede ser negativa.")
            .When(x => x.DailyDispatchGoal.HasValue);
    }
}
