using FluentValidation;
using ERP.Application.Common;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Interfaces;

namespace ERP.Application.Modules.Inventario.UseCases.ActualizarBodega;

public sealed class UpdateBodegaCommandValidator : AbstractValidator<UpdateBodegaCommand>
{
    public UpdateBodegaCommandValidator(
        IBodegaRepository repo,
        ICurrentTenant tenant)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID de la bodega es obligatorio.");

        RuleFor(x => x.SucursalId)
            .NotEmpty().WithMessage("La sucursal es obligatoria.");

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre de la bodega es obligatorio.")
            .MaximumLength(Bodega.NombreMaxLen)
            .WithMessage($"El nombre no puede exceder {Bodega.NombreMaxLen} caracteres.")
            .MustAsync(async (command, nombre, ct) =>
                !await repo.ExistsNombreAsync(tenant.TenantId, nombre, command.Id, ct))
            .WithMessage("Ya existe otra bodega con ese nombre en el tenant.");

        RuleFor(x => x.Ubicacion)
            .MaximumLength(Bodega.UbicacionMaxLen)
            .WithMessage($"La ubicación no puede exceder {Bodega.UbicacionMaxLen} caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Ubicacion));

        RuleFor(x => x.Encargado)
            .MaximumLength(Bodega.EncargadoMaxLen)
            .WithMessage($"El encargado no puede exceder {Bodega.EncargadoMaxLen} caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Encargado));
    }
}
