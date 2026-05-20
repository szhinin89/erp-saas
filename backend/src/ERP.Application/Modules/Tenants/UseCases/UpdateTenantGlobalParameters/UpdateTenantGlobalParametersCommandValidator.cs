using FluentValidation;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberGlobalParameters;

public sealed class UpdateSubscriberGlobalParametersCommandValidator : AbstractValidator<UpdateSubscriberGlobalParametersCommand>
{
    public UpdateSubscriberGlobalParametersCommandValidator()
    {
        RuleFor(x => x.SubscriberId)
            .NotEmpty().WithMessage("El tenant es obligatorio.");
    }
}
