using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.CreateCompanyUserPreferences;

/// <summary>
/// Crea las preferencias iniciales de una membresía. Falla si ya existen (relación 1:1,
/// invariante reforzado por ux_company_user_preferences_membership) — nunca actualiza.
/// </summary>
public sealed record CreateCompanyUserPreferencesCommand(
    Guid CompanyUserMembershipId,
    string LoginMode,
    Guid? DefaultBranchId
) : IRequest<Result<CompanyUserPreferencesDto>>;
