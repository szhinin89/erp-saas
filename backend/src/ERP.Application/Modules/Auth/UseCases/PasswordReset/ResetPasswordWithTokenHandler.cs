using System.Linq;
using FluentValidation;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Interfaces;

using MediatR;

namespace ERP.Application.Auth.UseCases.PasswordReset;

public sealed class ResetPasswordWithTokenHandler : IRequestHandler<ResetPasswordWithTokenCommand, Result<bool>>
{
    public const string InvalidTokenMessage = "El enlace de recuperación no es válido o ha expirado.";

    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IValidator<ResetPasswordWithTokenCommand> _validator;

    public ResetPasswordWithTokenHandler(
        IPasswordResetTokenRepository tokenRepository,
        IUserRepository userRepository,
        IAccessRepository accessRepository,
        ITenantRepository tenantRepository,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService,
        IValidator<ResetPasswordWithTokenCommand> validator)
    {
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _accessRepository = accessRepository;
        _tenantRepository = tenantRepository;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
        _validator = validator;
    }

    public async Task<Result<bool>> Handle(ResetPasswordWithTokenCommand command, CancellationToken ct)
    {
        var vr = await _validator.ValidateAsync(command, ct);
        if (!vr.IsValid)
            return Result<bool>.Failure(string.Join(" ", vr.Errors.Select(e => e.ErrorMessage)));

        var hash = PasswordResetTokenCrypto.Hash(command.Token.Trim());
        var stored = await _tokenRepository.GetByTokenHashAsync(hash, ct);
        if (stored is null || !stored.IsValid)
            return Result<bool>.Failure(InvalidTokenMessage);

        if (stored.UserKind != PasswordResetToken.KindSuperAdmin)
        {
            if (!stored.TenantId.HasValue)
                return Result<bool>.Failure(InvalidTokenMessage);

            if (!command.TenantId.HasValue || command.TenantId.Value != stored.TenantId.Value)
                return Result<bool>.Failure("El enlace de recuperación no coincide con la empresa indicada.");
        }

        var newHash = _passwordHasher.HashPassword(command.NewPassword);

        switch (stored.UserKind)
        {
            case PasswordResetToken.KindSuperAdmin:
            {
                var user = await _userRepository.GetByIdSystemAsync(stored.UserId, ct);
                if (user is null || !string.Equals(user.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
                    return Result<bool>.Failure(InvalidTokenMessage);

                user.SetPasswordHash(newHash, updatedBy: user.Id);
                await _userRepository.SaveChangesAsync(ct);
                await _refreshTokenService.RevokeAllForUserAsync(user.Id, Guid.Empty, "Cambio de contraseña (reset)", ct);
                break;
            }
            case PasswordResetToken.KindIdentity:
            {
                var identity = await _accessRepository.GetUserByIdAsync(stored.UserId, ct);
                if (identity is null)
                    return Result<bool>.Failure(InvalidTokenMessage);

                var tenantId = stored.TenantId!.Value;
                identity.SetPasswordHash(newHash, updatedBy: identity.Id);
                await _accessRepository.SaveChangesAsync(ct);
                await _refreshTokenService.RevokeAllForUserAsync(identity.Id, tenantId, "Cambio de contraseña (reset)", ct);
                break;
            }
            case PasswordResetToken.KindLegacy:
            {
                var tenantId = stored.TenantId!.Value;
                var legacy = await _userRepository.GetByIdAsync(stored.UserId, tenantId, ct);
                if (legacy is null)
                    return Result<bool>.Failure(InvalidTokenMessage);

                legacy.SetPasswordHash(newHash, updatedBy: legacy.Id);
                await _userRepository.SaveChangesAsync(ct);
                await _refreshTokenService.RevokeAllForUserAsync(legacy.Id, tenantId, "Cambio de contraseña (reset)", ct);
                break;
            }
            default:
                return Result<bool>.Failure(InvalidTokenMessage);
        }

        stored.MarkUsed();
        await _tokenRepository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
