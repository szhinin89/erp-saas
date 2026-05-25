using ERP.Application.Modules.Commercial.DTOs;
using FluentValidation;

namespace ERP.Application.Modules.Commercial.UseCases.ConvertQuoteToOrder;

public sealed class ConvertQuoteToOrderCommandValidator : AbstractValidator<ConvertQuoteToOrderCommand>
{
    public ConvertQuoteToOrderCommandValidator()
    {
        RuleFor(x => x.QuotePublicId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
    }
}
