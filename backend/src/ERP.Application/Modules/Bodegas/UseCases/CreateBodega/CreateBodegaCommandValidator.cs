using FluentValidation;
using ERP.Application.Common;
using ERP.Domain.Bodegas.Entities;
using ERP.Domain.Bodegas.Interfaces;

namespace ERP.Application.Modules.Bodegas.UseCases.CreateBodega;

public sealed class CreateBodegaCommandValidator : AbstractValidator<CreateBodegaCommand>
{
    public CreateBodegaCommandValidator(
        IBodegaRepository repo,
        ICurrentTenant tenant)
    {
        RuleFor(x => x.SucursalId)
            .NotEmpty().WithMessage("La sucursal es obligatoria.");

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre de la bodega es obligatorio.")
            .MaximumLength(Bodega.NombreMaxLen)
            .WithMessage($"El nombre no puede exceder {Bodega.NombreMaxLen} caracteres.")
            .MustAsync(async (nombre, ct) =>
                !await repo.ExistsNombreAsync(tenant.TenantId, nombre, null, ct))
            .WithMessage("Ya existe una bodega con ese nombre en el tenant.");

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
