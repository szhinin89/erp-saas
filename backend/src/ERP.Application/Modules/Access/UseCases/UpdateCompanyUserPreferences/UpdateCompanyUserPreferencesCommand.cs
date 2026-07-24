using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.UpdateCompanyUserPreferences;

/// <summary>
/// Modifica DefaultBranchId/LoginMode de preferencias ya existentes. Falla si todavía no
/// existen (usar CreateCompanyUserPreferences primero) — nunca hace upsert. Nunca toca
/// CompanyUserMembership.
/// </summary>
public sealed record UpdateCompanyUserPreferencesCommand(
    Guid CompanyUserMembershipId,
    string LoginMode,
    Guid? DefaultBranchId
) : IRequest<Result<CompanyUserPreferencesDto>>;
