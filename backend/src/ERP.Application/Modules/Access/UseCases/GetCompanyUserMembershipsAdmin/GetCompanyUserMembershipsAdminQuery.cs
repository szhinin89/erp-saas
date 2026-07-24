using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.GetCompanyUserMembershipsAdmin;

/// <summary>
/// Fase I-C. Lista las CompanyUserMembership de la empresa activa — a diferencia de
/// GetSecurityAdminMatrixQuery (Fase B), incluye inactivas cuando <see cref="OnlyActive"/> es
/// false (default) y expone ProfileId/ProfileName, que ningún endpoint anterior devolvía.
/// Fase S1 (hardening): <see cref="IRequiresCompanyContext"/> — antes de este fix, la única
/// defensa era el chequeo manual <c>_currentCompany.HasCompanyContext</c> en el handler, que
/// nunca revalida que la empresa del header <c>X-Company-Id</c> pertenezca realmente al tenant
/// del JWT ni que el llamador tenga una membership activa ahí (y ese chequeo manual queda sin
/// efecto para un caller con rol Admin, que obtiene el permiso sin pasar por
/// ICompanyAccessGuard). Este marker fuerza CompanyScopeBehavior → ICompanyAccessGuard.
/// RequireCurrentCompanyAsync antes de que el handler se ejecute — mismo mecanismo ya usado por
/// UpsertCompanyUserMembershipAdminCommand/RevokeCompanyUserMembershipAdminCommand (Fase I-A).
/// </summary>
public sealed record GetCompanyUserMembershipsAdminQuery(
    bool OnlyActive = false
) : IRequest<Result<IReadOnlyList<CompanyUserMembershipAdminDto>>>, IRequiresCompanyContext;
