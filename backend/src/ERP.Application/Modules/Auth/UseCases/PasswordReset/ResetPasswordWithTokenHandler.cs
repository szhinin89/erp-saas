using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Auth.UseCases.PasswordReset;

public sealed class ResetPasswordWithTokenHandler : IRequestHandler<ResetPasswordWithTokenCommand, Result<bool>>
{
    public const string InvalidTokenMessage = "El enlace de recuperación no es válido o ha expirado.";

    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IValidator<ResetPasswordWithTokenCommand> _validator;

    public ResetPasswordWithTokenHandler(
        IPasswordResetTokenRepository tokenRepository,
        IAccessRepository accessRepository,
        ITenantRepository TenantRepository,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService,
        IValidator<ResetPasswordWithTokenCommand> validator)
    {
        _tokenRepository = tokenRepository;
        _accessRepository = accessRepository;
        _tenantRepository = TenantRepository;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
        _validator = validator;
    }

    public async Task<Result<bool>> Handle(ResetPasswordWithTokenCommand command, CancellationToken cancellationToken)
    {
        var vr = await _validator.ValidateAsync(command, cancellationToken);
        if (!vr.IsValid)
            return Result<bool>.Failure(string.Join(" ", vr.Errors.Select(e => e.ErrorMessage)));

        var stored = await PasswordResetTokenConsumer.TryGetValidAsync(
            _tokenRepository, command.Token, PasswordResetToken.KindIdentity, cancellationToken);
        if (stored is null)
            return Result<bool>.Failure(InvalidTokenMessage);

        if (!stored.TenantId.HasValue)
            return Result<bool>.Failure(InvalidTokenMessage);

        if (!command.TenantId.HasValue || command.TenantId.Value != stored.TenantId.Value)
            return Result<bool>.Failure("El enlace de recuperación no coincide con la empresa indicada.");

        var identity = await PasswordResetTokenConsumer.ApplyAsync(
            _tokenRepository, _accessRepository, _passwordHasher, _refreshTokenService,
            stored, command.NewPassword, "Cambio de contraseña (reset)", cancellationToken);
        if (identity is null)
            return Result<bool>.Failure(InvalidTokenMessage);

        return Result<bool>.Success(true);
    }
}
