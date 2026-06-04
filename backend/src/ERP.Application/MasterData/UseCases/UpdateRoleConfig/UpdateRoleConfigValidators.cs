using ERP.Domain.MasterData.ValueObjects;
using FluentValidation;

namespace ERP.Application.MasterData.UseCases.UpdateRoleConfig;

public sealed class UpdateSupplierRoleConfigValidator : AbstractValidator<UpdateSupplierRoleConfigCommand>
{
    public UpdateSupplierRoleConfigValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId es obligatorio.");
        RuleFor(x => x.Config).NotNull().WithMessage("Config es obligatoria.");

        When(x => x.Config is not null, () =>
        {
            RuleFor(x => x.Config.DefaultTaxSupportCode)
                .MaximumLength(SupplierRoleConfig.SriCodeMaxLen).When(x => x.Config.DefaultTaxSupportCode is not null);
            RuleFor(x => x.Config.DefaultRetentionVatCode)
                .MaximumLength(SupplierRoleConfig.SriCodeMaxLen).When(x => x.Config.DefaultRetentionVatCode is not null);
            RuleFor(x => x.Config.DefaultRetentionIncomeCode)
                .MaximumLength(SupplierRoleConfig.SriCodeMaxLen).When(x => x.Config.DefaultRetentionIncomeCode is not null);
            RuleFor(x => x.Config.PaymentTerms)
                .MaximumLength(SupplierRoleConfig.PaymentTermsMaxLen).When(x => x.Config.PaymentTerms is not null);
        });
    }
}

public sealed class UpdateCarrierRoleConfigValidator : AbstractValidator<UpdateCarrierRoleConfigCommand>
{
    public UpdateCarrierRoleConfigValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId es obligatorio.");
        RuleFor(x => x.Config).NotNull().WithMessage("Config es obligatoria.");

        When(x => x.Config is not null, () =>
        {
            RuleFor(x => x.Config.TransportAuthorizationNumber)
                .MaximumLength(CarrierRoleConfig.AuthNumberMaxLen)
                .When(x => x.Config.TransportAuthorizationNumber is not null);
            RuleFor(x => x.Config.VehicleCapacityTons)
                .GreaterThan(0).WithMessage("La capacidad debe ser mayor a cero.")
                .When(x => x.Config.VehicleCapacityTons is not null);
        });
    }
}

public sealed class UpdateCustomerRoleConfigValidator : AbstractValidator<UpdateCustomerRoleConfigCommand>
{
    public UpdateCustomerRoleConfigValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId es obligatorio.");
        RuleFor(x => x.Config).NotNull().WithMessage("Config es obligatoria.");

        When(x => x.Config is not null, () =>
        {
            RuleFor(x => x.Config.CustomerCategory)
                .MaximumLength(CustomerRoleConfig.CategoryMaxLen)
                .Must(v => v is null || CustomerRoleConfig.ValidCategories.Contains(v))
                .WithMessage($"CustomerCategory debe ser uno de: {string.Join(", ", CustomerRoleConfig.ValidCategories)}")
                .When(x => x.Config.CustomerCategory is not null);

            RuleFor(x => x.Config.CustomerSegment)
                .MaximumLength(CustomerRoleConfig.SegmentMaxLen)
                .Must(v => v is null || CustomerRoleConfig.ValidSegments.Contains(v))
                .WithMessage($"CustomerSegment debe ser uno de: {string.Join(", ", CustomerRoleConfig.ValidSegments)}")
                .When(x => x.Config.CustomerSegment is not null);

            RuleFor(x => x.Config.SalesZone)
                .MaximumLength(CustomerRoleConfig.SalesZoneMaxLen)
                .When(x => x.Config.SalesZone is not null);

            RuleFor(x => x.Config.CreditRating)
                .MaximumLength(CustomerRoleConfig.CreditRatingMaxLen)
                .Must(v => v is null || CustomerRoleConfig.ValidCreditRatings.Contains(v))
                .WithMessage($"CreditRating debe ser uno de: {string.Join(", ", CustomerRoleConfig.ValidCreditRatings)}")
                .When(x => x.Config.CreditRating is not null);

            RuleFor(x => x.Config.LoyaltyTier)
                .MaximumLength(CustomerRoleConfig.LoyaltyTierMaxLen)
                .Must(v => v is null || CustomerRoleConfig.ValidLoyaltyTiers.Contains(v))
                .WithMessage($"LoyaltyTier debe ser uno de: {string.Join(", ", CustomerRoleConfig.ValidLoyaltyTiers)}")
                .When(x => x.Config.LoyaltyTier is not null);

            RuleFor(x => x.Config.PreferredInvoiceFormat)
                .MaximumLength(CustomerRoleConfig.InvoiceFormatMaxLen)
                .Must(v => v is null || CustomerRoleConfig.ValidInvoiceFormats.Contains(v))
                .WithMessage($"PreferredInvoiceFormat debe ser uno de: {string.Join(", ", CustomerRoleConfig.ValidInvoiceFormats)}")
                .When(x => x.Config.PreferredInvoiceFormat is not null);

            RuleFor(x => x.Config.CustomerClassification)
                .MaximumLength(CustomerRoleConfig.ClassificationMaxLen)
                .When(x => x.Config.CustomerClassification is not null);
        });
    }
}
