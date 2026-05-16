using FluentValidation;
using ERP.Application.Common;
using ERP.Domain.Common.Validators;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.CrearProveedor;

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator(
        ISupplierRepository repo,
        ICurrentTenant tenant)
    {
        RuleFor(x => x.PersonType)
            .NotEmpty().WithMessage("El tipo de persona es obligatorio.")
            .Must(t => t == Supplier.TypeNatural || t == Supplier.TypeLegal)
            .WithMessage($"PersonType debe ser '{Supplier.TypeNatural}' o '{Supplier.TypeLegal}'.");

        RuleFor(x => x.LegalName)
            .NotEmpty().WithMessage("La razón social es obligatoria.")
            .MaximumLength(Supplier.LegalNameMaxLen)
            .WithMessage($"La razón social no puede exceder {Supplier.LegalNameMaxLen} caracteres.");

        RuleFor(x => x.Ruc)
            .NotEmpty().WithMessage("El RUC es obligatorio.")
            .Length(Supplier.RucMaxLen).WithMessage("El RUC debe tener exactamente 13 dígitos.")
            .Must(ruc => RucValidator.EsRucValido(ruc))
            .WithMessage("El RUC no es válido según el algoritmo del SRI (módulo 10/11).")
            .MustAsync(async (ruc, ct) =>
                !await repo.ExistsRucAsync(tenant.TenantId, ruc, null, ct))
            .WithMessage("Ya existe un Supplier con ese RUC en el tenant.");

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
