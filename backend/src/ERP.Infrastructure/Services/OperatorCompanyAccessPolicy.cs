using ERP.Application.Common;
using ERP.Application.Common.Security;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;

namespace ERP.Infrastructure.Services;

public sealed class OperatorCompanyAccessPolicy : IOperatorCompanyAccessPolicy
{
    private readonly ICurrentOperatorContext _operatorContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessRepository _accessRepository;

    public OperatorCompanyAccessPolicy(
        ICurrentOperatorContext operatorContext,
        ICurrentUser currentUser,
        IAccessRepository accessRepository
    )
    {
        _operatorContext = operatorContext;
        _currentUser = currentUser;
        _accessRepository = accessRepository;
    }

    public async Task<bool> IsAuthorizedOperatorAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!_operatorContext.IsOperatorMode)
            return false;

        var globalAdminUserId = _operatorContext.GlobalAdminUserId;
        if (globalAdminUserId is null || globalAdminUserId != _currentUser.UserId)
            return false;

        var globalRole = await _accessRepository.GetActiveGlobalUserRoleAsync(
            globalAdminUserId.Value,
            SecurityRoles.Admin,
            cancellationToken
        );

        return globalRole is not null;
    }
}
