using System.Linq;
using FluentValidation;
using Microsoft.Extensions.Options;
using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Auth.UseCases.PasswordReset;

public sealed class ForgotPasswordHandler
{
    public const string NoAccountMessage = "No existe una cuenta con ese correo.";
    public const string MultipleAccountsMessage = "Hay múltiples cuentas con ese correo. Contacte a soporte.";
    public const string TenantResetDisabledMessage = "La recuperación de contraseña no está habilitada para esta empresa.";

    private readonly IUserRepository _userRepository;
    private readonly IAccessRepository _accessRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IPasswordResetLinkSender _linkSender;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IOptions<PasswordResetOptions> _options;
    private readonly IValidator<ForgotPasswordCommand> _validator;

    public ForgotPasswordHandler(
        IUserRepository userRepository,
        IAccessRepository accessRepository,
        ITenantRepository tenantRepository,
        IPasswordResetTokenRepository tokenRepository,
        IPasswordResetLinkSender linkSender,
        IDeploymentFeatureFlags deployment,
        IOptions<PasswordResetOptions> options,
        IValidator<ForgotPasswordCommand> validator)
    {
        _userRepository = userRepository;
        _accessRepository = accessRepository;
        _tenantRepository = tenantRepository;
        _tokenRepository = tokenRepository;
        _linkSender = linkSender;
        _deployment = deployment;
        _options = options;
        _validator = validator;
    }

    public async Task<Result<bool>> HandleAsync(ForgotPasswordCommand command, CancellationToken ct = default)
    {
        var vr = await _validator.ValidateAsync(command, ct);
        if (!vr.IsValid)
            return Result<bool>.Failure(string.Join(" ", vr.Errors.Select(e => e.ErrorMessage)));

        var email = command.Email.Trim().ToLowerInvariant();

        // 1) SuperAdmin (tabla users, rol global)
        var superAdmin = await _userRepository.GetSingleSuperAdminByEmailAsync(email, ct);
        if (superAdmin is not null)
        {
            if (!_deployment.IsSuperAdminPanelEnabled)
                return Result<bool>.Failure(NoAccountMessage);

            if (!superAdmin.IsActive)
                return Result<bool>.Failure(NoAccountMessage);

            await IssueTokenAndSendAsync(
                superAdmin.Id,
                null,
                PasswordResetToken.KindSuperAdmin,
                superAdmin.Email.Value,
                ct);
            return Result<bool>.Success(true);
        }

        // 2) Identity + memberships
        var identity = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (identity is not null)
        {
            if (!identity.IsActive)
                return Result<bool>.Failure(NoAccountMessage);

            var memberships = await _accessRepository.GetActiveMembershipsForUserSystemAsync(identity.Id, ct);
            if (memberships.Count == 0)
                return Result<bool>.Failure(NoAccountMessage);

            if (memberships.Count > 1)
                return Result<bool>.Failure(MultipleAccountsMessage);

            var tenant = await _tenantRepository.GetByIdAsync(memberships[0].TenantId, ct);
            if (tenant is null || !tenant.IsActive)
                return Result<bool>.Failure(NoAccountMessage);

            if (!TenantAllowsTokenReset(tenant.PasswordResetMode))
                return Result<bool>.Failure(TenantResetDisabledMessage);

            await IssueTokenAndSendAsync(
                identity.Id,
                tenant.Id,
                PasswordResetToken.KindIdentity,
                identity.Email.Value,
                ct);
            return Result<bool>.Success(true);
        }

        // 3) Legacy users por empresa
        var legacyMatches = await _userRepository.GetNonSuperAdminLegacyUsersByEmailAsync(email, ct);
        if (legacyMatches.Count == 0)
            return Result<bool>.Failure(NoAccountMessage);

        if (legacyMatches.Count > 1)
            return Result<bool>.Failure(MultipleAccountsMessage);

        var legacyUser = legacyMatches[0];
        var legacyTenant = await _tenantRepository.GetByIdAsync(legacyUser.TenantId, ct);
        if (legacyTenant is null || !legacyTenant.IsActive)
            return Result<bool>.Failure(NoAccountMessage);

        if (!TenantAllowsTokenReset(legacyTenant.PasswordResetMode))
            return Result<bool>.Failure(TenantResetDisabledMessage);

        await IssueTokenAndSendAsync(
            legacyUser.Id,
            legacyUser.TenantId,
            PasswordResetToken.KindLegacy,
            legacyUser.Email.Value,
            ct);
        return Result<bool>.Success(true);
    }

    private static bool TenantAllowsTokenReset(PasswordResetMode mode) =>
        mode is PasswordResetMode.Email or PasswordResetMode.Direct;

    private async Task IssueTokenAndSendAsync(
        Guid userId,
        Guid? tenantId,
        string userKind,
        string email,
        CancellationToken ct)
    {
        await _tokenRepository.InvalidateActiveForUserAsync(userId, userKind, tenantId, ct);

        var raw = PasswordResetTokenCrypto.CreateRawToken();
        var hash = PasswordResetTokenCrypto.Hash(raw);
        var minutes = Math.Clamp(_options.Value.TokenLifetimeMinutes, 5, 24 * 60);
        var expires = DateTime.UtcNow.AddMinutes(minutes);

        var entity = PasswordResetToken.Create(hash, userId, userKind, tenantId, expires);
        await _tokenRepository.AddAsync(entity, ct);
        await _tokenRepository.SaveChangesAsync(ct);

        var link = BuildResetLink(_options.Value.PublicBaseUrl, raw, tenantId);
        await _linkSender.SendPasswordResetLinkAsync(email, link, ct);
    }

    private static string BuildResetLink(string publicBaseUrl, string rawToken, Guid? tenantId)
    {
        var baseUrl = (publicBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
            baseUrl = "http://localhost:5173";

        var tokenEnc = Uri.EscapeDataString(rawToken);
        var qs = $"token={tokenEnc}";
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            qs += $"&tenantId={tenantId.Value}";

        return $"{baseUrl}/reset-password?{qs}";
    }
}
