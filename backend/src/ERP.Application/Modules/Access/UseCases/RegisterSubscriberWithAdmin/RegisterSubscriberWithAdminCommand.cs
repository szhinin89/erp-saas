using MediatR;
using ERP.Application.Common;
using ERP.Application.Access.DTOs;
using ERP.Domain.Subscribers.Entities;

namespace ERP.Application.Access.UseCases.RegisterSubscriberWithAdmin;

public record RegisterSubscriberWithAdminCommand(
    string SubscriberName,
    string SubscriberSlug,
    string AdminFirstName,
    string AdminLastName,
    string AdminEmail,
    string AdminPassword,
    PasswordResetMode PasswordResetMode = PasswordResetMode.Disabled,
    string? Ruc = null,
    string? ShortName = null,
    string? TradeName = null,
    string? Dinardap = null,
    string? LogoUrl = null,
    int DisplayOrder = 0,
    int Priority = 0,
    string? CountryCode = "ECU",
    string? Timezone = "America/Guayaquil"
) : IRequest<Result<SessionResponseDto>>;

