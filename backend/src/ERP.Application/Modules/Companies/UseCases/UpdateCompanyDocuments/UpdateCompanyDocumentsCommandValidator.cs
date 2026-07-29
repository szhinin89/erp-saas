using FluentValidation;

namespace ERP.Application.Modules.Companies.UseCases.UpdateCompanyDocuments;

public sealed class UpdateCompanyDocumentsCommandValidator
    : AbstractValidator<UpdateCompanyDocumentsCommand>
{
    public UpdateCompanyDocumentsCommandValidator()
    {
        RuleFor(x => x.ExtraLegend)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.ExtraLegend));
    }
}
