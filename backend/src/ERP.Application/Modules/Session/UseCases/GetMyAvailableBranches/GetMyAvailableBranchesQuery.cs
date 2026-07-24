using ERP.Application.Common;
using ERP.Application.Modules.Session.DTOs;
using MediatR;

namespace ERP.Application.Modules.Session.UseCases.GetMyAvailableBranches;

/// <summary>
/// Sin parámetros — siempre resuelve para (usuario actual, empresa operativa actual), igual
/// criterio que SwitchBranchCommand. ICompanyScopedRequest reutiliza CompanyScopeBehavior
/// (empresa operativa activa + membership válido) en vez de duplicar esa validación aquí.
/// </summary>
public sealed record GetMyAvailableBranchesQuery
    : IRequest<Result<MyAvailableBranchesDto>>, ICompanyScopedRequest;
