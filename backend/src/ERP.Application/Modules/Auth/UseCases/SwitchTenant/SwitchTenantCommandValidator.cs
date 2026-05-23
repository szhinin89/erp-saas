using FluentValidation;

namespace ERP.Application.Auth.UseCases.SwitchSubscriber;

public sealed class SwitchSubscriberCommandValidator : AbstractValidator<SwitchSubscriberCommand>
{
    public SwitchSubscriberCommandValidator()
    {
        // Guid.Empty is valid: operador platform sends it to return to the global panel.
        // Business validation (tenant exists, user is operador platform) is handled in the handler.
    }
}
