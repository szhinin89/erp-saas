using FluentValidation;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberSubscription;

public sealed class UpdateSubscriberSubscriptionCommandValidator : AbstractValidator<UpdateSubscriberSubscriptionCommand>
{
    public UpdateSubscriberSubscriptionCommandValidator()
    {
        RuleFor(x => x.SubscriberId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");

        RuleFor(x => x.PlanCode)
            .MaximumLength(64).WithMessage("El código de plan no puede exceder 64 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.PlanCode));

        When(x => x.EnabledModules is { Count: > 0 }, () =>
        {
            RuleForEach(x => x.EnabledModules!)
                .NotEmpty().WithMessage("Cada clave de módulo debe ser no vacía.")
                .MaximumLength(64).WithMessage("Cada clave de módulo no puede exceder 64 caracteres.");
        });
    }
}
