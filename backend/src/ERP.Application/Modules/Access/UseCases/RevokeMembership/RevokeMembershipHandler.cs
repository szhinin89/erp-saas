using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.RevokeMembership;

public class RevokeMembershipHandler
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentUser _currentUser;

    public RevokeMembershipHandler(IAccessRepository accessRepository, ICurrentUser currentUser)
    {
        _accessRepository = accessRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<object>> HandleAsync(RevokeMembershipCommand command, CancellationToken ct = default)
    {
        var email = command.UserEmail.Trim().ToLowerInvariant();
        var user = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (user is null)
            return Result<object>.Failure("Usuario no existe.");

        var membership = await _accessRepository.GetMembershipAsync(command.TenantId, user.Id, ct);
        if (membership is null || !membership.IsActive)
            return Result<object>.Success(new { });

        membership.Deactivate(_currentUser.UserId);
        await _accessRepository.SaveChangesAsync(ct);
        return Result<object>.Success(new { });
    }
}

