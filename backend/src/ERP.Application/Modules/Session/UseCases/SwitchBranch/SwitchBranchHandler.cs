using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Session.DTOs;
using MediatR;

namespace ERP.Application.Modules.Session.UseCases.SwitchBranch;

public sealed class SwitchBranchHandler
    : IRequestHandler<SwitchBranchCommand, Result<SessionBranchDto>>
{
    private readonly IBranchAccessGuard _branchAccessGuard;

    public SwitchBranchHandler(IBranchAccessGuard branchAccessGuard)
    {
        _branchAccessGuard = branchAccessGuard;
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
        return Result<SessionBranchDto>.Success(
            new SessionBranchDto(branch.BranchId, branch.BranchName, branch.IsMainBranch)
        );
    }
}
