using MediatR;
using ERP.Application.Common;
using ERP.Application.Access.DTOs;
using ERP.Domain.Subscribers.Entities;

namespace ERP.Application.Access.UseCases.PlatformSubscribers;

public record PlatformCreateSubscriberWithAdminCommand(
    string SubscriberName,
    string SubscriberSlug,
    string AdminFirstName,
    string AdminLastName,
    string AdminEmail,
    string AdminPassword,
    PasswordResetMode PasswordResetMode = PasswordResetMode.Disabled,
    int DisplayOrder = 0,
    int Priority = 0,
    bool LinkExistingAdmin = false,
    string? PlanCode = null,
    List<string>? EnabledModules = null,
    string? CountryCode = "ECU",
    string? Timezone = "America/Guayaquil"
) : IRequest<Result<SessionResponseDto>>;

public record GetPlatformSubscribersQuery : IRequest<Result<IReadOnlyList<PlatformSubscriberItemDto>>>;
