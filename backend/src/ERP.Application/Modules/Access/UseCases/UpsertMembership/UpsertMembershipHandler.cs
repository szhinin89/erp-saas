using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.UpsertMembership;

public class UpsertMembershipHandler
{
    private readonly IAccessRepository _accessRepository;
    private readonly ICurrentUser _currentUser;

    public UpsertMembershipHandler(IAccessRepository accessRepository, ICurrentUser currentUser)
    {
        _accessRepository = accessRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<object>> HandleAsync(UpsertMembershipCommand command, CancellationToken ct = default)
    {
        var email = command.UserEmail.Trim().ToLowerInvariant();
        var user = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (user is null)
            return Result<object>.Failure("Usuario no existe.");

        var existing = await _accessRepository.GetMembershipAsync(command.TenantId, user.Id, ct);
        if (existing is null)
        {
            var membership = Membership.Create(command.TenantId, user.Id, command.Role, command.ProfileId, createdBy: _currentUser.UserId);
            await _accessRepository.AddMembershipAsync(membership, ct);
        }
        else
        {
            existing.Activate(command.Role, command.ProfileId, updatedBy: _currentUser.UserId);
        }

        await _accessRepository.SaveChangesAsync(ct);
        return Result<object>.Success(new { });
    }
}

