using FluentValidation;

namespace ERP.Application.Sales.UseCases.RetencionesRecibidas;

public sealed class RegisterSalesRetentionCommandValidator : AbstractValidator<RegisterSalesRetentionCommand>
{
    public RegisterSalesRetentionCommandValidator()
    {
        RuleFor(x => x.SalesBillId).NotEmpty();
        RuleFor(x => x.XmlContent).NotEmpty().MaximumLength(512_000);
    }
}
