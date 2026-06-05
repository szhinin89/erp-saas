using FluentValidation;

namespace ERP.Application.Sales.UseCases.RetrySubmission;

public sealed class RetrySubmissionCommandValidator : AbstractValidator<RetrySubmissionCommand>
{
    public RetrySubmissionCommandValidator()
    {
        RuleFor(x => x.SaleId)
            .NotEmpty().WithMessage("El ID de la salesBill es obligatorio.");
    }
}
