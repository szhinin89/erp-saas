using System.Linq;
using FluentValidation;
using Microsoft.Extensions.Options;
using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.PasswordReset;

public sealed class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, Result<bool>>
{
    public const string NoAccountMessage = "No existe una cuenta con ese correo.";
    public const string MultipleAccountsMessage = "Hay múltiples cuentas con ese correo. Contacte a soporte.";
    public const string TenantResetDisabledMessage = "La recuperación de contraseña no está habilitada para esta empresa.";

    private readonly IAccessRepository _accessRepository;
    private readonly ISubscriberRepository _subscriberRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IPasswordResetLinkSender _linkSender;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly IOptions<PasswordResetOptions> _options;
    private readonly IValidator<ForgotPasswordCommand> _validator;

    public ForgotPasswordHandler(
        IAccessRepository accessRepository,
        ISubscriberRepository subscriberRepository,
        ICompanyRepository companyRepository,
        IPasswordResetTokenRepository tokenRepository,
        IPasswordResetLinkSender linkSender,
        IDeploymentFeatureFlags deployment,
        IOptions<PasswordResetOptions> options,
        IValidator<ForgotPasswordCommand> validator)
    {
        _accessRepository = accessRepository;
        _subscriberRepository = subscriberRepository;
        _companyRepository = companyRepository;
        _tokenRepository = tokenRepository;
        _linkSender = linkSender;
        _deployment = deployment;
        _options = options;
        _validator = validator;
    }

    public async Task<Result<bool>> Handle(ForgotPasswordCommand command, CancellationToken ct)
    {
        var vr = await _validator.ValidateAsync(command, ct);
        if (!vr.IsValid)
            return Result<bool>.Failure(string.Join(" ", vr.Errors.Select(e => e.ErrorMessage)));

        var email = command.Email.Trim().ToLowerInvariant();
        var identity = await _accessRepository.GetUserByEmailAsync(email, ct);
        if (identity is null)
            return Result<bool>.Failure(NoAccountMessage);

        if (!identity.IsActive)
            return Result<bool>.Failure(NoAccountMessage);

        if (identity.IsPlatformSuperAdmin)
        {
            if (!_deployment.IsSuperAdminPanelEnabled)
                return Result<bool>.Failure(NoAccountMessage);

            await IssueTokenAndSendAsync(
                identity.Id,
                null,
                PasswordResetToken.KindPlatform,
                identity.Email.Value,
                ct);
            return Result<bool>.Success(true);
        }

        var memberships = await _accessRepository.GetActiveCompanyUserMembershipsForUserSystemAsync(identity.Id, ct);
        if (memberships.Count == 0)
            return Result<bool>.Failure(NoAccountMessage);

        if (memberships.Count > 1)
        {
            var companyIds = memberships.Select(m => m.CompanyId).Distinct().ToList();
            var companies = await _companyRepository.GetByIdsAsync(companyIds, ct);
            if (companies.Select(c => c.SubscriberId).Distinct().Count() > 1)
                return Result<bool>.Failure(MultipleAccountsMessage);
        }

        var company = (await _companyRepository.GetByIdsAsync(
            new[] { memberships[0].CompanyId }, ct)).FirstOrDefault();
        var subscriberId = company?.SubscriberId ?? Guid.Empty;
        var tenant = await _subscriberRepository.GetByIdAsync(subscriberId, ct);
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

    private static bool TenantAllowsTokenReset(PasswordResetMode mode) =>
        mode is PasswordResetMode.Email or PasswordResetMode.Direct;

    private async Task IssueTokenAndSendAsync(
        Guid userId,
        Guid? subscriberId,
        string userKind,
        string email,
        CancellationToken ct)
    {
        await _tokenRepository.InvalidateActiveForUserAsync(userId, userKind, subscriberId, ct);

        var raw = PasswordResetTokenCrypto.CreateRawToken();
        var hash = PasswordResetTokenCrypto.Hash(raw);
        var minutes = Math.Clamp(_options.Value.TokenLifetimeMinutes, 5, 24 * 60);
        var expires = DateTime.UtcNow.AddMinutes(minutes);

        var entity = PasswordResetToken.Create(hash, userId, userKind, subscriberId, expires);
        await _tokenRepository.AddAsync(entity, ct);
        await _tokenRepository.SaveChangesAsync(ct);

        var link = BuildResetLink(_options.Value.PublicBaseUrl, raw, subscriberId);
        await _linkSender.SendPasswordResetLinkAsync(email, link, ct);
    }

    private static string BuildResetLink(string publicBaseUrl, string rawToken, Guid? subscriberId)
    {
        var baseUrl = (publicBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
            baseUrl = "http://localhost:5173";

        var tokenEnc = Uri.EscapeDataString(rawToken);
        var qs = $"token={tokenEnc}";
        if (subscriberId.HasValue && subscriberId.Value != Guid.Empty)
            qs += $"&subscriberId={subscriberId.Value}";

        return $"{baseUrl}/reset-password?{qs}";
    }
}
