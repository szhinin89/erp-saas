using FluentValidation;

namespace ERP.Application.Modules.Companies.UseCases.UpdateConsumerFinalMaxAmount;

public sealed class UpdateConsumerFinalMaxAmountCommandValidator
    : AbstractValidator<UpdateConsumerFinalMaxAmountCommand>
{
    public UpdateConsumerFinalMaxAmountCommandValidator()
    {
        RuleFor(x => x.ConsumerFinalMaxAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El monto máximo para Consumidor Final no puede ser negativo.");
    }
}
