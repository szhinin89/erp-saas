using FluentValidation;
using ERP.Application.Common;
using ERP.Domain.Common.Validators;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.ActualizarProveedor;

public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator(
        ISupplierRepository repo,
        ICurrentTenant tenant)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID del Supplier es obligatorio.");

        RuleFor(x => x.PersonType)
            .NotEmpty().WithMessage("El tipo de persona es obligatorio.")
            .Must(t => t == Supplier.TypeNatural || t == Supplier.TypeLegal)
            .WithMessage($"TipoPersona debe ser '{Supplier.TypeNatural}' o '{Supplier.TypeLegal}'.");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("La razón social es obligatoria.")
            .MaximumLength(Supplier.LegalNameMaxLen);

        RuleFor(x => x.Ruc)
            .NotEmpty().WithMessage("El RUC es obligatorio.")
            .Length(Supplier.RucMaxLen).WithMessage("El RUC debe tener exactamente 13 dígitos.")
            .Must(ruc => RucValidator.EsRucValido(ruc))
            .WithMessage("El RUC no es válido según el algoritmo del SRI (módulo 10/11).")
            .MustAsync(async (command, ruc, ct) =>
                !await repo.ExistsRucAsync(tenant.TenantId, ruc, command.Id, ct))
            .WithMessage("Ya existe otro Supplier con ese RUC en el tenant.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El correo electrónico no es válido.")
            .MaximumLength(Supplier.EmailMaxLen)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Phone)
            .MaximumLength(Supplier.PhoneMaxLen)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Address)
            .MaximumLength(Supplier.AddressMaxLen)
            .When(x => !string.IsNullOrWhiteSpace(x.Address));

        RuleFor(x => x.PaymentTerms)
            .NotEmpty().WithMessage("La condición de pago es obligatoria.")
            .MaximumLength(Supplier.PaymentTermsMaxLen);
    }
}
