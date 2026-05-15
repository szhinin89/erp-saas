using FluentValidation;
using ERP.Application.Common;
using ERP.Domain.Common.Validators;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.ActualizarProveedor;

public sealed class UpdateProveedorCommandValidator : AbstractValidator<UpdateProveedorCommand>
{
    public UpdateProveedorCommandValidator(
        IProveedorRepository repo,
        ICurrentTenant tenant)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID del proveedor es obligatorio.");

        RuleFor(x => x.TipoPersona)
            .NotEmpty().WithMessage("El tipo de persona es obligatorio.")
            .Must(t => t == Proveedor.TipoNatural || t == Proveedor.TipoJuridica)
            .WithMessage($"TipoPersona debe ser '{Proveedor.TipoNatural}' o '{Proveedor.TipoJuridica}'.");

        RuleFor(x => x.RazonSocial)
            .NotEmpty().WithMessage("La razón social es obligatoria.")
            .MaximumLength(Proveedor.RazonSocialMaxLen);

        RuleFor(x => x.Ruc)
            .NotEmpty().WithMessage("El RUC es obligatorio.")
            .Length(Proveedor.RucMaxLen).WithMessage("El RUC debe tener exactamente 13 dígitos.")
            .Must(ruc => RucValidator.EsRucValido(ruc))
            .WithMessage("El RUC no es válido según el algoritmo del SRI (módulo 10/11).")
            .MustAsync(async (command, ruc, ct) =>
                !await repo.ExistsRucAsync(tenant.TenantId, ruc, command.Id, ct))
            .WithMessage("Ya existe otro proveedor con ese RUC en el tenant.");

        RuleFor(x => x.Correo)
            .EmailAddress().WithMessage("El correo electrónico no es válido.")
            .MaximumLength(Proveedor.CorreoMaxLen)
            .When(x => !string.IsNullOrWhiteSpace(x.Correo));

        RuleFor(x => x.Telefono)
            .MaximumLength(Proveedor.TelefonoMaxLen)
            .When(x => !string.IsNullOrWhiteSpace(x.Telefono));

        RuleFor(x => x.Direccion)
            .MaximumLength(Proveedor.DireccionMaxLen)
            .When(x => !string.IsNullOrWhiteSpace(x.Direccion));

        RuleFor(x => x.CondicionPago)
            .NotEmpty().WithMessage("La condición de pago es obligatoria.")
            .MaximumLength(Proveedor.CondicionPagoMaxLen);
    }
}
