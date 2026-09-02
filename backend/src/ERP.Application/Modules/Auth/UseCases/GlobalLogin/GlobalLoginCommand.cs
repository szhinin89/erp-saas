using ERP.Application.Auth.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Auth.UseCases.GlobalLogin;

public sealed record GlobalLoginCommand(string Username, string Password)
    : IRequest<Result<AuthResponseDto>>;
