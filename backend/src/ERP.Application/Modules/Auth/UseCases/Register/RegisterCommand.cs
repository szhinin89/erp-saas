using MediatR;
using ERP.Application.Common;
using ERP.Application.Auth.DTOs;

namespace ERP.Application.Auth.UseCases.Register;

public record RegisterCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Guid SubscriberId,
    string Role = "User"
) : IRequest<Result<AuthResponseDto>>;
