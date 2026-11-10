using ERP.Application.Common;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentValidation;

namespace ERP.Application.Modules.OrgConfig.UseCases.UpsertCompanyInvoiceOrgSettings;

/// <summary>
/// Vacío o solo espacios = "sin configurar" (el handler lo normaliza a null). Los valores
/// informados se validan contra <see cref="IInvoiceDefaultsCatalogValidator"/> — regla única
/// compartida con la lectura administrativa y con <see cref="IInvoiceDefaultsResolver"/>.
/// </summary>
public sealed class UpsertCompanyInvoiceOrgSettingsCommandValidator
    : AbstractValidator<UpsertCompanyInvoiceOrgSettingsCommand>
{
    public UpsertCompanyInvoiceOrgSettingsCommandValidator(
        IInvoiceDefaultsCatalogValidator catalog,
        ICurrentTenant tenant
    )
    {
        When(
            c => !string.IsNullOrWhiteSpace(c.DefaultDocTypeCode),
            () =>
                RuleFor(c => c.DefaultDocTypeCode)
                    .Cascade(CascadeMode.Stop)
                    .MaximumLength(SriSettings.DocTypeCodeMaxLen)
                    .WithMessage(
                        $"El código de tipo de documento no puede superar {SriSettings.DocTypeCodeMaxLen} caracteres."
                    )
                    .MustAsync((v, ct) => catalog.IsValidDocTypeCodeAsync(v!.Trim(), ct))
                    .WithMessage(InvoiceDefaultsCatalogMessages.InvalidDocType)
        );

        When(
            c => !string.IsNullOrWhiteSpace(c.DefaultSriPaymentMethodCode),
            () =>
                RuleFor(c => c.DefaultSriPaymentMethodCode)
                    .Cascade(CascadeMode.Stop)
                    .MaximumLength(SriSettings.PaymentMethodCodeMaxLen)
                    .WithMessage(
                        $"El código de forma de pago SRI no puede superar {SriSettings.PaymentMethodCodeMaxLen} caracteres."
                    )
                    .MustAsync((v, ct) => catalog.IsValidSriPaymentMethodCodeAsync(v!.Trim(), ct))
                    .WithMessage(InvoiceDefaultsCatalogMessages.InvalidSriPaymentMethod)
        );

        When(
            c => c.DefaultPaymentTermId.HasValue,
            () =>
                RuleFor(c => c.DefaultPaymentTermId)
                    .MustAsync(
                        (v, ct) => catalog.IsValidPaymentTermAsync(tenant.TenantId, v!.Value, ct)
                    )
                    .WithMessage(InvoiceDefaultsCatalogMessages.InvalidPaymentTerm)
        );
    }
}
