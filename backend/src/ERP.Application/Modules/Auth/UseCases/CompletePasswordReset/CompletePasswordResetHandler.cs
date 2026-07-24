using System.Linq;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using FluentValidation;
using MediatR;

namespace ERP.Application.Auth.UseCases.CompletePasswordReset;

public sealed class CompletePasswordResetHandler : IRequestHandler<CompletePasswordResetCommand, Result<AuthResponseDto>>
{
    public const string InvalidTokenMessage = "El token de restablecimiento no es válido o ha expirado.";

    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IMediator _mediator;
    private readonly IValidator<CompletePasswordResetCommand> _validator;

    public CompletePasswordResetHandler(
        IPasswordResetTokenRepository tokenRepository,
        IAccessRepository accessRepository,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService,
        IMediator mediator,
        IValidator<CompletePasswordResetCommand> validator)
    {
        _tokenRepository = tokenRepository;
        _accessRepository = accessRepository;
        _passwordHasher = passwordHasher;
        _refreshTokenService = refreshTokenService;
        _mediator = mediator;
        _validator = validator;
    }

    public async Task<Result<AuthResponseDto>> Handle(CompletePasswordResetCommand command, CancellationToken cancellationToken)
    {
        var vr = await _validator.ValidateAsync(command, cancellationToken);
        if (!vr.IsValid)
            return Result<AuthResponseDto>.Failure(string.Join(" ", vr.Errors.Select(e => e.ErrorMessage)));

        var stored = await PasswordResetTokenConsumer.TryGetValidAsync(
            _tokenRepository, command.PasswordResetToken, PasswordResetToken.KindIdentity, cancellationToken);
        if (stored is null)
            return Result<AuthResponseDto>.Failure(InvalidTokenMessage);

        var identity = await PasswordResetTokenConsumer.ApplyAsync(
            _tokenRepository, _accessRepository, _passwordHasher, _refreshTokenService,
            stored, command.NewPassword, "Cambio de contraseña (primer inicio de sesión)", cancellationToken);
        if (identity is null)
            return Result<AuthResponseDto>.Failure(InvalidTokenMessage);

        // Reautentica con la contraseña recién establecida — reutiliza LoginHandler íntegro
        // (resolución de tenant/empresa, selección multi-empresa, JWT, refresh, UserSession).
        // RequirePasswordReset ya quedó en false (PasswordResetTokenConsumer.ApplyAsync llama
        // explícitamente a IdentityUser.ClearRequirePasswordReset), así que este Login no vuelve
        // a caer en la rama de reset.
        return await _mediator.Send(new LoginCommand(identity.Username, command.NewPassword), cancellationToken);
    }
}
