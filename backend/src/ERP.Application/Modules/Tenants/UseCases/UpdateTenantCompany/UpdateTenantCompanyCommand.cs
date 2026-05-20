using MediatR;
using ERP.Application.Common;
using ERP.Application.Subscribers.DTOs;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberCompany;

public record UpdateSubscriberCompanyCommand(
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
