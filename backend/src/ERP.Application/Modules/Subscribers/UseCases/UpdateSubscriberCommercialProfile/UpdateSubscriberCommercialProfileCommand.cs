using MediatR;
using ERP.Application.Common;
using ERP.Application.Subscribers.DTOs;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberCommercialProfile;

public record UpdateSubscriberCommercialProfileCommand(
    Guid SubscriberId,
    string Name,
    string Slug,
    int DisplayOrder,
    int Priority,
    string PreferredLanguage = "es") : IRequest<Result<SubscriberDto>>;
