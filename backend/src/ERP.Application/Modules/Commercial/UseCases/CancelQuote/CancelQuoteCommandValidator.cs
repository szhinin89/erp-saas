using FluentValidation;

namespace ERP.Application.Modules.Commercial.UseCases.CancelQuote;

public sealed class CancelQuoteCommandValidator : AbstractValidator<CancelQuoteCommand>
{
    public CancelQuoteCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
    }
}
