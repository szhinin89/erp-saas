using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Session.DTOs;
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

        // Best-effort: mantiene UserSession.BranchId coherente con el switch para el fallback de
        // GetSessionContextHandler cuando el cliente aún no envía X-Branch-Id — nunca es la fuente
        // de autorización (eso siempre pasa por ICurrentBranch + BranchScopeBehavior por request).
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
            await _userSessions.SaveChangesAsync(cancellationToken);
        }

        return Result<SessionBranchDto>.Success(
            new SessionBranchDto(branch.BranchId, branch.BranchName, branch.IsMainBranch)
        );
    }
}
