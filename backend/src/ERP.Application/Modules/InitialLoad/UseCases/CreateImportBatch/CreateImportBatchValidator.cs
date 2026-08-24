using FluentValidation;

namespace ERP.Application.Modules.InitialLoad.UseCases.CreateImportBatch;

public sealed class CreateImportBatchValidator : AbstractValidator<CreateImportBatchCommand>
{
    public CreateImportBatchValidator()
    {
        RuleFor(x => x.ImportType).IsInEnum();
        RuleFor(x => x.Label).MaximumLength(200).When(x => x.Label is not null);
    }
}
