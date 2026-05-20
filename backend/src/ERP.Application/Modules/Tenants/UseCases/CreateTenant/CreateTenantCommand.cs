using MediatR;
using ERP.Application.Common;
using ERP.Application.Subscribers.DTOs;

namespace ERP.Application.Subscribers.UseCases.CreateSubscriber;

public record CreateSubscriberCommand(
    string Name,
    string Slug,
    ERP.Domain.Subscribers.Entities.PasswordResetMode PasswordResetMode = ERP.Domain.Subscribers.Entities.PasswordResetMode.Disabled,
    string? Ruc = null,
    string? ShortName = null,
    string? TradeName = null,
    string? Dinardap = null,
    string? LogoUrl = null,
    int DisplayOrder = 0,
    int Priority = 0,
    string? PlanCode = null,
    IReadOnlyList<string>? EnabledModules = null
) : IRequest<Result<SubscriberDto>>;
