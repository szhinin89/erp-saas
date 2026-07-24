using FluentValidation;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyFiscal;

public sealed class UpdateCompanyFiscalCommandValidator : AbstractValidator<UpdateCompanyFiscalCommand>
{
    public UpdateCompanyFiscalCommandValidator()
    {
        RuleFor(x => x.TaxRegimeCode).MaximumLength(5).When(x => !string.IsNullOrWhiteSpace(x.TaxRegimeCode));
        RuleFor(x => x.SpecialTaxpayerNo).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.SpecialTaxpayerNo));
    }
}
