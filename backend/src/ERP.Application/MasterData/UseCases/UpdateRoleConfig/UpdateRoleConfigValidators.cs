using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.MasterData.ValueObjects;
using FluentValidation;

namespace ERP.Application.MasterData.UseCases.UpdateRoleConfig;

public sealed class UpdateSupplierRoleConfigValidator
    : AbstractValidator<UpdateSupplierRoleConfigCommand>
{
    public UpdateSupplierRoleConfigValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId es obligatorio.");
        RuleFor(x => x.Config).NotNull().WithMessage("Config es obligatoria.");

        When(
            x => x.Config is not null,
            () =>
            {
                RuleFor(x => x.Config.PaymentTermId)
                    .NotEmpty()
                    .WithMessage("El proveedor debe tener una condición de pago obligatoria.");
                RuleFor(x => x.Config.DefaultTaxSupportCode)
                    .MaximumLength(SupplierRoleConfig.SriCodeMaxLen)
                    .When(x => x.Config.DefaultTaxSupportCode is not null);
                RuleFor(x => x.Config.DefaultRetentionVatCode)
                    .MaximumLength(SupplierRoleConfig.SriCodeMaxLen)
                    .When(x => x.Config.DefaultRetentionVatCode is not null);
                RuleFor(x => x.Config.DefaultRetentionIncomeCode)
                    .MaximumLength(SupplierRoleConfig.SriCodeMaxLen)
                    .When(x => x.Config.DefaultRetentionIncomeCode is not null);
                RuleFor(x => x.Config.DefaultPaymentMethodCode)
                    .Must(v => v is null || SupplierRoleConfig.ValidPaymentMethodCodes.Contains(v))
                    .WithMessage(
                        $"DefaultPaymentMethodCode debe ser uno de: {string.Join(", ", SupplierRoleConfig.ValidPaymentMethodCodes)}"
                    )
                    .When(x => x.Config.DefaultPaymentMethodCode is not null);
                RuleFor(x => x.Config.RefundProviderTypeCode)
                    .MaximumLength(SupplierRoleConfig.SriCodeMaxLen)
                    .When(x => x.Config.RefundProviderTypeCode is not null);
            }
        );
    }
}

/// <summary>
/// CLASS-BP-CATALOGS-01: los antiguos <c>.Must(v =&gt; ValidX.Contains(v))</c> síncronos contra un
/// <c>HashSet&lt;string&gt;</c> fijo en Domain se reemplazan por <c>.MustAsync(...)</c> contra los
/// 6 catálogos persistidos de proveedor (tenant+company-scoped). Primer uso de <c>MustAsync</c> en
/// el repo — funciona sin cambios de infraestructura porque <c>ValidationBehavior</c> ya invoca
/// <c>ValidateAsync</c> (no <c>Validate</c>) vía <c>Task.WhenAll</c>.
///
/// La empresa activa se resuelve de <see cref="ICurrentCompany"/> (contexto autenticado, nunca del
/// body) — la edición de clasificación de proveedor siempre ocurre dentro de una sesión con
/// empresa activa en el frontend. Sin empresa activa, <c>CodeExistsActiveAsync</c> no matchea
/// ninguna fila (fail-closed), igual que cualquier otra consulta company-scoped del ERP.
/// </summary>
public sealed class UpdateSupplierClassificationConfigValidator
    : AbstractValidator<UpdateSupplierClassificationConfigCommand>
{
    public UpdateSupplierClassificationConfigValidator(
        ISupplierCategoryRepository categoryRepo,
        ISupplierTypeRepository typeRepo,
        ISupplierRiskRepository riskRepo,
        ISupplierRatingRepository ratingRepo,
        IPrimaryGoodTypeRepository goodTypeRepo,
        ISupplierSegmentRepository segmentRepo,
        ERP.Application.Common.ICurrentTenant tenant,
        ERP.Application.Common.ICurrentCompany company
    )
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId es obligatorio.");
        RuleFor(x => x.Config).NotNull().WithMessage("Config es obligatoria.");

        When(
            x => x.Config is not null,
            () =>
            {
                RuleFor(x => x.Config.SupplierCategory)
                    .MaximumLength(SupplierClassificationConfig.CategoryMaxLen)
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
                        "SupplierCategory no corresponde a un valor activo del catálogo de la empresa."
                    )
                    .When(x => x.Config.SupplierCategory is not null);
                RuleFor(x => x.Config.SupplierType)
                    .MaximumLength(SupplierClassificationConfig.TypeMaxLen)
                    .MustAsync(
                        (v, ct) =>
                            typeRepo.CodeExistsActiveAsync(
                                tenant.TenantId,
                                company.CompanyId,
                                v!,
                                ct
                            )
                    )
                    .WithMessage(
                        "SupplierType no corresponde a un valor activo del catálogo de la empresa."
                    )
                    .When(x => x.Config.SupplierType is not null);
                RuleFor(x => x.Config.SupplierRisk)
                    .MaximumLength(SupplierClassificationConfig.RiskMaxLen)
                    .MustAsync(
                        (v, ct) =>
                            riskRepo.CodeExistsActiveAsync(
                                tenant.TenantId,
                                company.CompanyId,
                                v!,
                                ct
                            )
                    )
                    .WithMessage(
                        "SupplierRisk no corresponde a un valor activo del catálogo de la empresa."
                    )
                    .When(x => x.Config.SupplierRisk is not null);
                RuleFor(x => x.Config.SupplierRating)
                    .MaximumLength(SupplierClassificationConfig.RatingMaxLen)
                    .MustAsync(
                        (v, ct) =>
                            ratingRepo.CodeExistsActiveAsync(
                                tenant.TenantId,
                                company.CompanyId,
                                v!,
                                ct
                            )
                    )
                    .WithMessage(
                        "SupplierRating no corresponde a un valor activo del catálogo de la empresa."
                    )
                    .When(x => x.Config.SupplierRating is not null);
                RuleFor(x => x.Config.PrimaryGoodType)
                    .MaximumLength(SupplierClassificationConfig.GoodTypeMaxLen)
                    .MustAsync(
                        (v, ct) =>
                            goodTypeRepo.CodeExistsActiveAsync(
                                tenant.TenantId,
                                company.CompanyId,
                                v!,
                                ct
                            )
                    )
                    .WithMessage(
                        "PrimaryGoodType no corresponde a un valor activo del catálogo de la empresa."
                    )
                    .When(x => x.Config.PrimaryGoodType is not null);
                RuleFor(x => x.Config.SupplierSegment)
                    .MaximumLength(SupplierClassificationConfig.SegmentMaxLen)
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
                        "SupplierSegment no corresponde a un valor activo del catálogo de la empresa."
                    )
                    .When(x => x.Config.SupplierSegment is not null);
                RuleFor(x => x.Config.PaymentMethodPreference)
                    .MaximumLength(SupplierClassificationConfig.PaymentPrefMaxLen)
                    .When(x => x.Config.PaymentMethodPreference is not null);
            }
        );
    }
}

public sealed class UpdateCarrierRoleConfigValidator
    : AbstractValidator<UpdateCarrierRoleConfigCommand>
{
    public UpdateCarrierRoleConfigValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId es obligatorio.");
        RuleFor(x => x.Config).NotNull().WithMessage("Config es obligatoria.");

        When(
            x => x.Config is not null,
            () =>
            {
                RuleFor(x => x.Config.TransportAuthorizationNumber)
                    .MaximumLength(CarrierRoleConfig.AuthNumberMaxLen)
                    .When(x => x.Config.TransportAuthorizationNumber is not null);
                RuleFor(x => x.Config.VehicleCapacityTons)
                    .GreaterThan(0)
                    .WithMessage("La capacidad debe ser mayor a cero.")
                    .When(x => x.Config.VehicleCapacityTons is not null);
            }
        );
    }
}

/// <summary>
/// CLASS-BP-CATALOGS-01: mismo cambio que <see cref="UpdateSupplierClassificationConfigValidator"/>
/// para los 6 catálogos de cliente. <c>CustomerClassification</c> tiene un bypass explícito
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
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId es obligatorio.");
        RuleFor(x => x.Config).NotNull().WithMessage("Config es obligatoria.");

        When(
            x => x.Config is not null,
            () =>
            {
                RuleFor(x => x.Config.CustomerCategory)
                    .MaximumLength(CustomerRoleConfig.CategoryMaxLen)
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
                    .When(x => x.Config.CustomerCategory is not null);

                RuleFor(x => x.Config.CustomerSegment)
                    .MaximumLength(CustomerRoleConfig.SegmentMaxLen)
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
                    .When(x => x.Config.CustomerSegment is not null);

                RuleFor(x => x.Config.SalesZone)
                    .MaximumLength(CustomerRoleConfig.SalesZoneMaxLen)
                    .When(x => x.Config.SalesZone is not null);

                RuleFor(x => x.Config.CreditRating)
                    .MaximumLength(CustomerRoleConfig.CreditRatingMaxLen)
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
                    .When(x => x.Config.CreditRating is not null);

                RuleFor(x => x.Config.LoyaltyTier)
                    .MaximumLength(CustomerRoleConfig.LoyaltyTierMaxLen)
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
                    .When(x => x.Config.LoyaltyTier is not null);

                RuleFor(x => x.Config.PreferredInvoiceFormat)
                    .MaximumLength(CustomerRoleConfig.InvoiceFormatMaxLen)
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
                    .When(x => x.Config.PreferredInvoiceFormat is not null);

                RuleFor(x => x.Config.CustomerClassification)
                    .MaximumLength(CustomerRoleConfig.ClassificationMaxLen)
                    .MustAsync(
                        async (cmd, v, ct) =>
                        {
                            var role = await roleRepo.GetByIdAsync(cmd.RoleId, ct);
                            var currentValue = role?.CustomerConfig?.CustomerClassification;
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
                    .When(x => x.Config.CustomerClassification is not null);
            }
        );
    }
}
