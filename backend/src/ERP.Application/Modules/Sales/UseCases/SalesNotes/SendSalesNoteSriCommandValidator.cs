using FluentValidation;

namespace ERP.Application.Sales.UseCases.SalesNotes;

public sealed class SendSalesNoteSriCommandValidator : AbstractValidator<SendSalesNoteSriCommand>
{
    public SendSalesNoteSriCommandValidator()
    {
        RuleFor(x => x.NotaId).NotEmpty();
    }
}
