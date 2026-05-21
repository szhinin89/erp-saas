using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Platform.Auth.UseCases.PlatformLogin;

public sealed record PlatformLoginCommand(
    string Email,
    string Password
) : IRequest<Result<AuthResponseDto>>;
