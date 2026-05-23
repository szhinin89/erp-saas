using MediatR;
using ERP.Application.Common;
using ERP.Application.Auth.DTOs;

namespace ERP.Application.Auth.UseCases.ClaimInitialPlatformOperator;

/// <summary>
/// Crea el único SuperAdmin en <c>users</c> con <c>subscriber_id = Guid.Empty</c> (operador de plataforma, sin empresa previa).
/// Las empresas se crean después con el panel SuperAdmin autenticado.
/// </summary>
public sealed record ClaimInitialPlatformOperatorCommand(
    string SetupToken,
    string FirstName,
    string LastName,
    string Email,
    string Password) : IRequest<Result<AuthResponseDto>>;
