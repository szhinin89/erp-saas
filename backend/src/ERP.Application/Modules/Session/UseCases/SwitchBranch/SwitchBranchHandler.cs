using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Session.DTOs;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Session.UseCases.SwitchBranch;

public sealed class SwitchBranchHandler
    : IRequestHandler<SwitchBranchCommand, Result<SessionBranchDto>>
{
    private readonly IBranchAccessGuard _branchAccessGuard;
    private readonly IUserSessionRepository _userSessions;

    public SwitchBranchHandler(
        IBranchAccessGuard branchAccessGuard,
        IUserSessionRepository userSessions
    )
    {
        _branchAccessGuard = branchAccessGuard;
        _userSessions = userSessions;
    }

    public async Task<Result<SessionBranchDto>> Handle(
        SwitchBranchCommand request,
        CancellationToken cancellationToken
    )
    {
        var access = await _branchAccessGuard.RequireBranchAsync(
            request.BranchId,
            cancellationToken
        );
        if (!access.IsSuccess)
            return Result<SessionBranchDto>.Failure(access.Error!);

        var branch = access.Value!;

        // Persiste UserSession.BranchId como contexto operativo de la sesión — nunca es la
        // fuente de autorización (eso siempre pasa por ICurrentBranch + BranchScopeBehavior por
        // request, ya validado arriba vía IBranchAccessGuard). Es el único respaldo server-side
        // de la sucursal elegida: activeBranchStore (frontend) vive en sessionStorage, aislado
        // por pestaña, así que sin esto una pestaña nueva — o cualquier limpieza del store en la
        // misma pestaña — no tiene forma de recuperar la selección y vuelve a pedir sucursal.
        //
        // ERP-CORE-BRANCH-SESSION-PERSISTENCE-01: si no hay UserSession activa para esta empresa
        // (caso típico de login en LoginMode.AskBranch — ver LoginHandler, que deliberadamente no
        // crea una cuando no puede resolver la sucursal en el login), se crea aquí. Antes esto era
        // un no-op silencioso pese a que LoginHandler documentaba lo contrario.
        var activeSessions = await _userSessions.GetActiveSessionsAsync(
            branch.UserId,
            branch.TenantId,
            cancellationToken
        );
        var currentSession = activeSessions.FirstOrDefault(s => s.CompanyId == branch.CompanyId);
        if (currentSession is not null)
        {
            currentSession.UpdateBranch(branch.BranchId, branch.UserId);
            await _userSessions.UpdateAsync(currentSession, cancellationToken);
        }
        else
        {
            var newSession = UserSession.Create(
                branch.TenantId,
                branch.CompanyId,
                branch.UserId,
                branch.BranchId,
                LoginHandler.UnresolvedTerminalId
            );
            await _userSessions.AddAsync(newSession, cancellationToken);
        }
        await _userSessions.SaveChangesAsync(cancellationToken);

        return Result<SessionBranchDto>.Success(
            new SessionBranchDto(branch.BranchId, branch.BranchName, branch.IsMainBranch)
        );
    }
}
