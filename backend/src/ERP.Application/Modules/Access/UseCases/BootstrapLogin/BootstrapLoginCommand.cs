using MediatR;
using ERP.Application.Common;
using ERP.Application.Access.DTOs;

namespace ERP.Application.Access.UseCases.BootstrapLogin;

public record BootstrapLoginCommand(
    string Email,
    string Password
) : IRequest<Result<BootstrapLoginResponseDto>>;

