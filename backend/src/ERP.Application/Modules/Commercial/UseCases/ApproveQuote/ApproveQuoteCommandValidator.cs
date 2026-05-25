using FluentValidation;

namespace ERP.Application.Modules.Commercial.UseCases.ApproveQuote;

public sealed class ApproveQuoteCommandValidator : AbstractValidator<ApproveQuoteCommand>
{
    public ApproveQuoteCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
    }
}
