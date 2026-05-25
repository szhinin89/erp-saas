using FluentValidation;

namespace ERP.Application.Modules.Commercial.UseCases.CancelSalesOrder;

public sealed class CancelSalesOrderCommandValidator : AbstractValidator<CancelSalesOrderCommand>
{
    public CancelSalesOrderCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
    }
}
