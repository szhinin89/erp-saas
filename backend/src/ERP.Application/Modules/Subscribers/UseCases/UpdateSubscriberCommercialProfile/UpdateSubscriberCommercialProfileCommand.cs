using MediatR;
using ERP.Application.Common;
using ERP.Application.Subscribers.DTOs;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberCommercialProfile;

public record UpdateSubscriberCommercialProfileCommand(
    Guid SubscriberId,
    string Name,
    string Slug,
    string? Ruc,
    string? ShortName,
    string? TradeName,
    string? Dinardap,
    string? LogoUrl,
    int DisplayOrder,
    int Priority) : IRequest<Result<SubscriberDto>>;
