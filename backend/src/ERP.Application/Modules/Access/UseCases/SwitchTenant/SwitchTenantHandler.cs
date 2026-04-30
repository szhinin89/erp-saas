using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.UseCases.SwitchTenant;

public class SwitchTenantHandler
{
    private readonly IAccessRepository _accessRepository;
    private readonly IAccessTokenService _tokenService;
    private readonly ICurrentUser _currentUser;

    public SwitchTenantHandler(
        IAccessRepository accessRepository,
        IAccessTokenService tokenService,
        ICurrentUser currentUser)
    {
        _accessRepository = accessRepository;
        _tokenService = tokenService;
        _currentUser = currentUser;
    }

    public async Task<Result<SessionResponseDto>> HandleAsync(SwitchTenantCommand command, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<SessionResponseDto>.Failure("Unauthorized");

        var userId = _currentUser.UserId;
        if (userId == Guid.Empty)
            return Result<SessionResponseDto>.Failure("Unauthorized");

        var user = await _accessRepository.GetUserByIdAsync(userId, ct);
        if (user is null)
        {
            // SuperAdmin puede operar sin memberships (acceso global).
            var role = _currentUser.Role;
            var email = _currentUser.Email;
            var fullName = _currentUser.FullName;

            if (!string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(fullName))
                return Result<SessionResponseDto>.Failure("Unauthorized");

            var superSessionToken = _tokenService.GenerateSessionToken(userId, email, fullName, command.TenantId, "SuperAdmin");

            return Result<SessionResponseDto>.Success(new SessionResponseDto(
                UserId: userId,
                FullName: fullName,
                Email: email,
                TenantId: command.TenantId,
                Role: "SuperAdmin",
                Token: superSessionToken));
        }

        if (!user.IsActive)
            return Result<SessionResponseDto>.Failure("Unauthorized");

        var membership = await _accessRepository.GetMembershipAsync(command.TenantId, user.Id, ct);
        if (membership is null || !membership.IsActive)
            return Result<SessionResponseDto>.Failure("No tienes acceso a esta empresa.");

        var membershipSessionToken = _tokenService.GenerateSessionToken(user, command.TenantId, membership.Role);

        return Result<SessionResponseDto>.Success(new SessionResponseDto(
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email.Value,
            TenantId: command.TenantId,
            Role: membership.Role,
            Token: membershipSessionToken));
    }
}

