using FluentValidation;

namespace ERP.Application.Access.UseCases.SwitchSubscriber;

public sealed class SwitchSubscriberCommandValidator : AbstractValidator<SwitchSubscriberCommand>
{
    public SwitchSubscriberCommandValidator()
    {
        RuleFor(x => x.SubscriberId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");
    }
}
