using ERP.Application.Common;
using ERP.Application.Modules.Session.DTOs;
using MediatR;

namespace ERP.Application.Modules.Session.UseCases.GetSessionContext;

/// <summary>
/// Empresa operativa opcional: se resuelve internamente (header X-Company-Id o
/// membresía única). No usar ICompanyScopedRequest/IRequiresCompanyContext aquí —
/// la sesión debe poder consultarse incluso antes de seleccionar empresa.
/// </summary>
public sealed record GetSessionContextQuery
    : IRequest<Result<SessionContextDto>>, ITenantScopedRequest;
