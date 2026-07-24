using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Setup.CreateInitialAdmin;

public sealed record CreateInitialAdminCommand(
    string Username,
    string FirstName,
    string LastName,
    string? Email,
    string Password,
    string SetupToken) : IRequest<Result<string>>;
