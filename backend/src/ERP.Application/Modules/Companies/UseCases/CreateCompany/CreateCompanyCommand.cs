using ERP.Application.Common;
using ERP.Application.Modules.Companies.DTOs;
using MediatR;

namespace ERP.Application.Modules.Companies.UseCases.CreateCompany;

/// <summary>
/// Datos del administrador inicial de la empresa (IAM-V1-F3). No lleva contraseña: el backend
/// genera una temporal criptográficamente segura y la devuelve una sola vez.
/// </summary>
public sealed record InitialAdministratorRequest(
    string Username,
    string FirstName,
    string LastName,
    string Email
);

public sealed record CreateCompanyCommand(
    Guid TenantId,
    string TaxId,
    string LegalName,
    string? TradeName,
    InitialAdministratorRequest? InitialAdministrator = null
) : IRequest<Result<CompanyCreatedDto>>;
