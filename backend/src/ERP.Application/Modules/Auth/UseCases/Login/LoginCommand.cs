using MediatR;
using ERP.Application.Common;
using ERP.Application.Auth.DTOs;

namespace ERP.Application.Auth.UseCases.Login;

public record LoginCommand(
    string Email,
    string Password
) : IRequest<Result<AuthResponseDto>>;
