using ERP.Application.MasterData.Services;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using FluentValidation;

namespace ERP.Application.MasterData.UseCases.UpdateRoleConfig;

/// <summary>
/// SUPPLIER-SRI-CATALOGS-01: los códigos SRI de <see cref="SupplierRoleConfig"/> se validan de
/// forma async contra sus catálogos reales (globales, sin tenant/company scope) — reemplaza el
/// antiguo <c>HashSet&lt;string&gt;</c> fijo de <c>DefaultPaymentMethodCode</c> en Domain y agrega
/// validación de catálogo (antes inexistente) para los otros 2 campos.
///
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01: DefaultRetentionVatCode/DefaultRetentionIncomeCode se
/// eliminaron de este VO — la validación de códigos de retención contra
/// <see cref="ERP.Application.Modules.Purchases.Services.IRetentionCodeResolver"/> ahora vive en
/// UpsertSupplierRetentionDefaultValidator, sobre la lista dinámica.
/// </summary>
/// <remarks>
/// ZH-API-THIN-BP-ROLES-01: el command trae la config como DTO primitivo. Las reglas validan la
/// config normalizada por Domain (<see cref="RoleConfigFactory"/>: trim, vacío → null) con las
/// mismas claves de error (<c>Config.X</c>) que cuando el controller construía el VO; si un
/// invariante de Domain falla, las reglas de config no corren y el handler responde el 400 histórico.
/// </remarks>
public sealed class UpdateSupplierRoleConfigValidator
    : AbstractValidator<UpdateSupplierRoleConfigCommand>
{
    public UpdateSupplierRoleConfigValidator(ISriCatalogLookupRepository catalogRepo)
    {
        RuleFor(x => x.BusinessPartnerId)
            .NotEmpty()
            .WithMessage("BusinessPartnerId es obligatorio.");
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId es obligatorio.");
        RuleFor(x => x.Config).NotNull().WithMessage("Config es obligatoria.");

        When(
            x => Normalized(x) is not null,
            () =>
            {
                RuleFor(x => Normalized(x)!.DefaultTaxSupportCode)
                    .MaximumLength(SupplierRoleConfig.SriCodeMaxLen)
                    .OverridePropertyName("Config.DefaultTaxSupportCode")
                    .MustAsync((v, ct) => catalogRepo.TaxSupportCodeExistsActiveAsync(v!, ct))
                    .WithMessage(
                        "DefaultTaxSupportCode no corresponde a un código activo del catálogo sri_tax_support."
                    )
                    .When(x => Normalized(x)!.DefaultTaxSupportCode is not null);
                RuleFor(x => Normalized(x)!.DefaultPaymentMethodCode)
                    .MaximumLength(SupplierRoleConfig.SriCodeMaxLen)
                    .OverridePropertyName("Config.DefaultPaymentMethodCode")
                    .MustAsync((v, ct) => catalogRepo.PaymentMethodCodeExistsActiveAsync(v!, ct))
                    .WithMessage(
                        "DefaultPaymentMethodCode no corresponde a un código activo del catálogo sri_payment_method."
                    )
                    .When(x => Normalized(x)!.DefaultPaymentMethodCode is not null);
                RuleFor(x => Normalized(x)!.RefundProviderTypeCode)
                    .MaximumLength(SupplierRoleConfig.SriCodeMaxLen)
                    .OverridePropertyName("Config.RefundProviderTypeCode")
                    .MustAsync((v, ct) => catalogRepo.SupplierTypeCodeExistsActiveAsync(v!, ct))
                    .WithMessage(
                        "RefundProviderTypeCode no corresponde a un código activo del catálogo sri_supplier_type."
                    )
                    .When(x => Normalized(x)!.RefundProviderTypeCode is not null);
            }
        );
    }

    /// <summary>Config ya normalizada por Domain; null si viola un invariante (lo responde el handler).</summary>
    private static SupplierRoleConfig? Normalized(UpdateSupplierRoleConfigCommand x) =>
        x.Config is null ? null : RoleConfigFactory.Build(x.Config).Config;
}

public sealed class UpdateCarrierRoleConfigValidator
    : AbstractValidator<UpdateCarrierRoleConfigCommand>
{
    public UpdateCarrierRoleConfigValidator()
    {
        RuleFor(x => x.BusinessPartnerId)
            .NotEmpty()
            .WithMessage("BusinessPartnerId es obligatorio.");
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId es obligatorio.");
        RuleFor(x => x.Config).NotNull().WithMessage("Config es obligatoria.");

        When(
            x => Normalized(x) is not null,
            () =>
            {
                RuleFor(x => Normalized(x)!.TransportAuthorizationNumber)
                    .MaximumLength(CarrierRoleConfig.AuthNumberMaxLen)
                    .OverridePropertyName("Config.TransportAuthorizationNumber")
                    .When(x => Normalized(x)!.TransportAuthorizationNumber is not null);
                RuleFor(x => Normalized(x)!.VehicleCapacityTons)
                    .GreaterThan(0)
                    .OverridePropertyName("Config.VehicleCapacityTons")
                    .WithMessage("La capacidad debe ser mayor a cero.")
                    .When(x => Normalized(x)!.VehicleCapacityTons is not null);
            }
        );
    }

    /// <summary>Config ya normalizada por Domain; null si viola un invariante (lo responde el handler).</summary>
    private static CarrierRoleConfig? Normalized(UpdateCarrierRoleConfigCommand x) =>
        x.Config is null ? null : RoleConfigFactory.Build(x.Config).Config;
}

/// <summary>
/// CLASS-BP-CATALOGS-01: validación async (<c>MustAsync</c>) contra los catálogos persistidos
/// de cliente (tenant+company-scoped), reemplazando cualquier <c>HashSet&lt;string&gt;</c> fijo en
/// Domain. <c>CustomerClassification</c> tiene un bypass explícito
/// (decisión ya confirmada por el usuario, ver plan): si el valor enviado es igual al valor
/// actualmente almacenado en BD para ese rol, no se exige que esté en el catálogo — protege
/// registros legacy. Solo se valida contra el catálogo cuando el usuario efectivamente cambia el
/// valor. Auditoría previa (`SELECT DISTINCT customer_classification FROM
/// master_bp_customer_configs WHERE customer_classification IS NOT NULL`, ejecutada contra la BD
/// local dberpsaas el 2026-08-08): 0 filas — no hay valores legacy fuera del catálogo sembrado hoy.
/// </summary>
public sealed class UpdateCustomerRoleConfigValidator
    : AbstractValidator<UpdateCustomerRoleConfigCommand>
{
    public UpdateCustomerRoleConfigValidator(
        ICustomerCategoryRepository categoryRepo,
        ICustomerSegmentRepository segmentRepo,
        ICustomerCreditRatingRepository creditRatingRepo,
        ILoyaltyTierRepository loyaltyTierRepo,
        ICustomerInvoiceFormatRepository invoiceFormatRepo,
        ICustomerClassificationRepository classificationRepo,
        IBusinessPartnerRoleRepository roleRepo,
        ERP.Application.Common.ICurrentTenant tenant,
        ERP.Application.Common.ICurrentCompany company
    )
    {
        RuleFor(x => x.BusinessPartnerId)
            .NotEmpty()
            .WithMessage("BusinessPartnerId es obligatorio.");
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId es obligatorio.");
        RuleFor(x => x.Config).NotNull().WithMessage("Config es obligatoria.");

        When(
            x => Normalized(x) is not null,
            () =>
            {
                RuleFor(x => Normalized(x)!.CustomerCategory)
                    .MaximumLength(CustomerRoleConfig.CategoryMaxLen)
                    .OverridePropertyName("Config.CustomerCategory")
                    .MustAsync(
                        (v, ct) =>
                            categoryRepo.CodeExistsActiveAsync(
                                tenant.TenantId,
                                company.CompanyId,
                                v!,
                                ct
                            )
                    )
                    .WithMessage(
                        "CustomerCategory no corresponde a un valor activo del catálogo de la empresa."
                    )
                    .When(x => Normalized(x)!.CustomerCategory is not null);

                RuleFor(x => Normalized(x)!.CustomerSegment)
                    .MaximumLength(CustomerRoleConfig.SegmentMaxLen)
                    .OverridePropertyName("Config.CustomerSegment")
                    .MustAsync(
                        (v, ct) =>
                            segmentRepo.CodeExistsActiveAsync(
                                tenant.TenantId,
                                company.CompanyId,
                                v!,
                                ct
                            )
                    )
                    .WithMessage(
                        "CustomerSegment no corresponde a un valor activo del catálogo de la empresa."
                    )
                    .When(x => Normalized(x)!.CustomerSegment is not null);

                RuleFor(x => Normalized(x)!.SalesZone)
                    .MaximumLength(CustomerRoleConfig.SalesZoneMaxLen)
                    .OverridePropertyName("Config.SalesZone")
                    .When(x => Normalized(x)!.SalesZone is not null);

                RuleFor(x => Normalized(x)!.CreditRating)
                    .MaximumLength(CustomerRoleConfig.CreditRatingMaxLen)
                    .OverridePropertyName("Config.CreditRating")
                    .MustAsync(
                        (v, ct) =>
                            creditRatingRepo.CodeExistsActiveAsync(
                                tenant.TenantId,
                                company.CompanyId,
                                v!,
                                ct
                            )
                    )
                    .WithMessage(
                        "CreditRating no corresponde a un valor activo del catálogo de la empresa."
                    )
                    .When(x => Normalized(x)!.CreditRating is not null);

                RuleFor(x => Normalized(x)!.LoyaltyTier)
                    .MaximumLength(CustomerRoleConfig.LoyaltyTierMaxLen)
                    .OverridePropertyName("Config.LoyaltyTier")
                    .MustAsync(
                        (v, ct) =>
                            loyaltyTierRepo.CodeExistsActiveAsync(
                                tenant.TenantId,
                                company.CompanyId,
                                v!,
                                ct
                            )
                    )
                    .WithMessage(
                        "LoyaltyTier no corresponde a un valor activo del catálogo de la empresa."
                    )
                    .When(x => Normalized(x)!.LoyaltyTier is not null);

                RuleFor(x => Normalized(x)!.PreferredInvoiceFormat)
                    .MaximumLength(CustomerRoleConfig.InvoiceFormatMaxLen)
                    .OverridePropertyName("Config.PreferredInvoiceFormat")
                    .MustAsync(
                        (v, ct) =>
                            invoiceFormatRepo.CodeExistsActiveAsync(
                                tenant.TenantId,
                                company.CompanyId,
                                v!,
                                ct
                            )
                    )
                    .WithMessage(
                        "PreferredInvoiceFormat no corresponde a un valor activo del catálogo de la empresa."
                    )
                    .When(x => Normalized(x)!.PreferredInvoiceFormat is not null);

                RuleFor(x => Normalized(x)!.CustomerClassification)
                    .MaximumLength(CustomerRoleConfig.ClassificationMaxLen)
                    .OverridePropertyName("Config.CustomerClassification")
                    .MustAsync(
                        async (cmd, v, ct) =>
                        {
                            // Solo un rol del BP de la ruta aporta su valor actual: un rol de otro
                            // BP se trata como inexistente (no revela existencia ni valor guardado).
                            var role = await roleRepo.GetByIdAsync(cmd.RoleId, ct);
                            var currentValue =
                                role?.BusinessPartnerId == cmd.BusinessPartnerId
                                    ? role.CustomerConfig?.CustomerClassification
                                    : null;
                            if (
                                currentValue is not null
                                && string.Equals(currentValue, v, StringComparison.Ordinal)
                            )
                                return true; // bypass: valor sin cambios respecto a BD (legacy)

                            return await classificationRepo.CodeExistsActiveAsync(
                                tenant.TenantId,
                                company.CompanyId,
                                v!,
                                ct
                            );
                        }
                    )
                    .WithMessage(
                        "CustomerClassification no corresponde a un valor activo del catálogo de la empresa."
                    )
                    .When(x => Normalized(x)!.CustomerClassification is not null);
            }
        );
    }

    /// <summary>Config ya normalizada por Domain; null si viola un invariante (lo responde el handler).</summary>
    private static CustomerRoleConfig? Normalized(UpdateCustomerRoleConfigCommand x) =>
        x.Config is null ? null : RoleConfigFactory.Build(x.Config).Config;
}
