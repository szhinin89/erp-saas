using ERP.Domain.Configuration.Entities;
using FluentValidation;

namespace ERP.Application.Modules.OrgConfig.UseCases.UpsertCompanyInvoiceOrgSettings;

public sealed class UpsertCompanyInvoiceOrgSettingsCommandValidator
    : AbstractValidator<UpsertCompanyInvoiceOrgSettingsCommand>
{
    public UpsertCompanyInvoiceOrgSettingsCommandValidator()
    {
        When(c => !string.IsNullOrWhiteSpace(c.DefaultDocTypeCode), () =>
            RuleFor(c => c.DefaultDocTypeCode)
                .MaximumLength(SriSettings.DocTypeCodeMaxLen)
                .WithMessage($"El código de tipo de documento no puede superar {SriSettings.DocTypeCodeMaxLen} caracteres."));

        When(c => !string.IsNullOrWhiteSpace(c.DefaultSriPaymentMethodCode), () =>
            RuleFor(c => c.DefaultSriPaymentMethodCode)
                .MaximumLength(SriSettings.PaymentMethodCodeMaxLen)
                .WithMessage($"El código de forma de pago SRI no puede superar {SriSettings.PaymentMethodCodeMaxLen} caracteres."));
    }
}
