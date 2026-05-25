using FluentValidation;

namespace ERP.Application.Access.UseCases.BootstrapSwitchSubscriber;

public sealed class BootstrapSwitchSubscriberCommandValidator : AbstractValidator<BootstrapSwitchSubscriberCommand>
{
    public BootstrapSwitchSubscriberCommandValidator()
    {
        RuleFor(x => x.SubscriberId).NotEmpty();
    }
}
