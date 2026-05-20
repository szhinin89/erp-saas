using FluentValidation;

namespace ERP.Application.Auth.UseCases.SwitchSubscriber;

public sealed class SwitchSubscriberCommandValidator : AbstractValidator<SwitchSubscriberCommand>
{
    public SwitchSubscriberCommandValidator()
    {
        // Guid.Empty is valid: SuperAdmin sends it to return to the global panel.
        // Business validation (tenant exists, user is SuperAdmin) is handled in the handler.
    }
}
