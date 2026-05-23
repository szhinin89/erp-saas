using System.Linq;
using FluentValidation;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.PasswordReset;

public sealed class ResetPasswordWithTokenHandler : IRequestHandler<ResetPasswordWithTokenCommand, Result<bool>>
{
    public const string InvalidTokenMessage = "El enlace de recuperación no es válido o ha expirado.";

    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IValidator<ResetPasswordWithTokenCommand> _validator;

    public ResetPasswordWithTokenHandler(
        IPasswordResetTokenRepository tokenRepository,
        IAccessRepository accessRepository,
        ISubscriberRepository subscriberRepository,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService,
        IValidator<ResetPasswordWithTokenCommand> validator)
    {
        _tokenRepository = tokenRepository;
        _accessRepository = accessRepository;
        _subscriberRepository = subscriberRepository;
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

        var isPlatformKind = stored.UserKind is PasswordResetToken.KindPlatform
            or PasswordResetToken.KindPlatformOperator
            or PasswordResetToken.KindLegacyPlatformOperator;
        if (!isPlatformKind)
        {
            if (!stored.SubscriberId.HasValue)
                return Result<bool>.Failure(InvalidTokenMessage);

            if (!command.SubscriberId.HasValue || command.SubscriberId.Value != stored.SubscriberId.Value)
                return Result<bool>.Failure("El enlace de recuperación no coincide con la empresa indicada.");
        }

        var newHash = _passwordHasher.HashPassword(command.NewPassword);
        var identity = await _accessRepository.GetUserByIdAsync(stored.UserId, ct);
        if (identity is null)
            return Result<bool>.Failure(InvalidTokenMessage);

        if (isPlatformKind && !identity.IsPrimaryPlatformOperator)
            return Result<bool>.Failure(InvalidTokenMessage);

        if (stored.UserKind == PasswordResetToken.KindLegacy)
            return Result<bool>.Failure(InvalidTokenMessage);

        identity.SetPasswordHash(newHash, updatedBy: identity.Id);
        await _accessRepository.SaveChangesAsync(ct);

        var revokeSubscriberId = stored.SubscriberId ?? Guid.Empty;
        await _refreshTokenService.RevokeAllForUserAsync(
            identity.Id, revokeSubscriberId, "Cambio de contraseña (reset)", ct);

        stored.MarkUsed();
        await _tokenRepository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
