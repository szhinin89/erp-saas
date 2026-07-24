using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.GetUserSessionsPaged;

/// <summary>
/// Dashboard administrativo (Fase 10) — ITenantScopedRequest (no ICompanyScopedRequest):
/// un administrador debe poder ver sesiones de varias empresas del mismo tenant a la vez;
/// CompanyId es un filtro opcional, no el scope automático de empresa activa.
/// </summary>
public sealed record GetUserSessionsPagedQuery(
    Guid? IdentityUserId = null,
    Guid? CompanyId = null,
    string? Status = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int PageNumber = 1,
    int PageSize = 25
) : IRequest<Result<PagedResult<UserSessionAdminDto>>>, ITenantScopedRequest;
