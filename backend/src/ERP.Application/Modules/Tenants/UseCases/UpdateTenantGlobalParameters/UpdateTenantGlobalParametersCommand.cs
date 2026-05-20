using MediatR;
using ERP.Application.Common;
using ERP.Application.Subscribers.DTOs;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberGlobalParameters;

public record UpdateSubscriberGlobalParametersCommand(
    Guid SubscriberId,
    bool ElectronicBillingTrialEnabled) : IRequest<Result<SubscriberDto>>;
