using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;
using FluentValidation;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyFiscal;

/// <summary>
/// Validación de formato (form-validation.md, nivel backend). Los formatos salen de las reglas de
/// <see cref="CompanyEntity"/> — la misma fuente que aplica el dominio y que revisa Readiness.
/// </summary>
public sealed class UpdateCompanyFiscalCommandValidator
    : AbstractValidator<UpdateCompanyFiscalCommand>
{
    public UpdateCompanyFiscalCommandValidator()
    {
        RuleFor(x => x.TaxRegimeCode)
            .MaximumLength(5)
            .When(x => !string.IsNullOrWhiteSpace(x.TaxRegimeCode));
        RuleFor(x => x.SpecialTaxpayerNo)
            .Must(v => CompanyEntity.IsValidSpecialTaxpayerNo(v!.Trim()))
            .WithMessage(CompanyEntity.SpecialTaxpayerNoFormatMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.SpecialTaxpayerNo));

        // Anexo 21: se valida la forma normalizada (sin ceros a la izquierda) que guarda el dominio.
        RuleFor(x => x.RetentionAgentResolutionNo)
            .Must(v => CompanyEntity.IsValidRetentionAgentResolutionNo(
                CompanyEntity.NormalizeRetentionAgentResolutionNo(v)
            ))
            .WithMessage(CompanyEntity.RetentionAgentDesignationMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.RetentionAgentResolutionNo));
        RuleFor(x => x.RetentionAgentEffectiveFrom)
            .NotNull()
            .WithMessage(CompanyEntity.RetentionAgentDesignationMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.RetentionAgentResolutionNo));
        RuleFor(x => x.RetentionAgentResolutionNo)
            .NotEmpty()
            .WithMessage(CompanyEntity.RetentionAgentDesignationMessage)
            .When(x => x.RetentionAgentEffectiveFrom is not null);

        RuleFor(x => x.LargeTaxpayerResolutionNo)
            .Must(v => CompanyEntity.IsValidLargeTaxpayerResolutionNo(v!.Trim()))
            .WithMessage(CompanyEntity.LargeTaxpayerDesignationMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.LargeTaxpayerResolutionNo));
        RuleFor(x => x.LargeTaxpayerEffectiveFrom)
            .NotNull()
            .WithMessage(CompanyEntity.LargeTaxpayerDesignationMessage)
            .When(x => !string.IsNullOrWhiteSpace(x.LargeTaxpayerResolutionNo));
        RuleFor(x => x.LargeTaxpayerResolutionNo)
            .NotEmpty()
            .WithMessage(CompanyEntity.LargeTaxpayerDesignationMessage)
            .When(x => x.LargeTaxpayerEffectiveFrom is not null);
    }
}
