using MediatR;
using ERP.Application.Common;
using ERP.Application.Auth.DTOs;

namespace ERP.Application.Auth.UseCases.SuperAdminLogin;

public record SuperAdminLoginCommand(
    string Email,
    string Password
) : IRequest<Result<AuthResponseDto>>;
