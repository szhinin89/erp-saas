using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.CreateSystemUserAdmin;

/// <summary>
/// Envoltorio administrativo de <see cref="ERP.Application.Access.UseCases.CreateSystemUser.CreateSystemUserCommand"/>:
/// nunca acepta TenantId/CompanyId del cliente, los resuelve del contexto autenticado
/// (ICurrentTenant/ICurrentCompany) antes de delegar — mismo principio que
/// <c>UpsertCompanyUserMembershipAdminCommand</c>.
/// </summary>
public sealed record CreateSystemUserAdminCommand(
    string Username,
    string FirstName,
    string LastName,
    string? Email,
    string Password,
    string Role,
    Guid? ProfileId = null
) : IRequest<Result<CreateSystemUserResultDto>>, IRequiresCompanyContext;
