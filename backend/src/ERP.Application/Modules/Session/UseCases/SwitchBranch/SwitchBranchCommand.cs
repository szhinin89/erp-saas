using ERP.Application.Common;
using ERP.Application.Modules.Session.DTOs;
using MediatR;

namespace ERP.Application.Modules.Session.UseCases.SwitchBranch;

/// <summary>
/// Selección de sucursal activa: delega la validación completa (empresa operativa, sucursal
/// existente/activa/de la empresa, y CompanyUserBranch activa para el usuario actual) en
/// IBranchAccessGuard — misma fuente única de verdad que usa BranchScopeBehavior para proteger
/// IBranchScopedRequest. No reemite el JWT — el frontend adopta el resultado adjuntando
/// X-Branch-Id en las siguientes peticiones, igual que switch-company con X-Company-Id.
/// ICompanyScopedRequest reutiliza CompanyScopeBehavior ya existente (empresa operativa activa
/// + membership válido) en vez de duplicar esa validación aquí.
/// </summary>
public sealed record SwitchBranchCommand(Guid BranchId)
    : IRequest<Result<SessionBranchDto>>,
        ICompanyScopedRequest;
