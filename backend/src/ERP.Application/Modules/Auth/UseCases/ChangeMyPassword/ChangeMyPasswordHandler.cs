using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.ChangeMyPassword;

public sealed class ChangeMyPasswordHandler : IRequestHandler<ChangeMyPasswordCommand, Result<bool>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly IPasswordHasher _hasher;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRefreshTokenService _refreshTokenService;

    public ChangeMyPasswordHandler(
        IAccessRepository accessRepository,
        IPasswordHasher hasher,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IRefreshTokenService refreshTokenService
    )
    {
        _accessRepository = accessRepository;
        _hasher = hasher;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<bool>> Handle(
        ChangeMyPasswordCommand command,
        CancellationToken cancellationToken
    )
    {
        var user = await _accessRepository.GetUserByIdAsync(_currentUser.UserId, cancellationToken);
        if (user is null)
            return Result<bool>.Failure("Usuario no encontrado.");

        if (!_hasher.VerifyPassword(command.CurrentPassword, user.PasswordHash))
            return Result<bool>.Failure("La contraseña actual no es correcta.");

        var newHash = _hasher.HashPassword(command.NewPassword);
        user.SetPasswordHash(newHash, updatedBy: user.Id);
        user.ClearRequirePasswordReset(updatedBy: user.Id);
        await _accessRepository.SaveChangesAsync(cancellationToken);

        // Mismo mecanismo y misma consecuencia que el reset por token (ResetPasswordWithTokenHandler):
        // cambiar la contraseña invalida todas las sesiones existentes, incluida la actual.
        await _refreshTokenService.RevokeAllForUserAsync(
            user.Id,
            _currentTenant.TenantId,
            "Cambio de contraseña (self-service)",
            cancellationToken
        );

        return Result<bool>.Success(true);
    }
}
