using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Application.Auth.UseCases.PasswordReset;

/// <summary>
/// ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01 — la respuesta pública es siempre la misma
/// (<c>Success(true)</c>) para cuenta existente, inexistente, inactiva, sin membresía activa, con
/// tenants ambiguos o con el cupo por identidad agotado: el endpoint no permite enumerar cuentas.
/// El motivo real solo se registra internamente (<see cref="PasswordResetSuppressionReason"/>),
/// sin email, token ni enlace. Solo el formato inválido del email (no depende de la cuenta) y los
/// fallos técnicos reales (BD, caché) salen por el contrato de errores habitual.
/// Auth sigue siendo dueño del token (<see cref="PasswordResetTokenIssuer"/>).
/// </summary>
public sealed partial class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, Result<bool>>
{
    private readonly IAccessRepository _accessRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IPasswordResetLinkSender _linkSender;
    private readonly IPasswordResetRequestThrottle _throttle;
    private readonly IOptions<PasswordResetOptions> _options;
    private readonly IValidator<ForgotPasswordCommand> _validator;
    private readonly ILogger<ForgotPasswordHandler> _logger;

    public ForgotPasswordHandler(
        IAccessRepository accessRepository,
        ITenantRepository TenantRepository,
        ICompanyRepository companyRepository,
        IPasswordResetTokenRepository tokenRepository,
        IPasswordResetLinkSender linkSender,
        IPasswordResetRequestThrottle throttle,
        IOptions<PasswordResetOptions> options,
        IValidator<ForgotPasswordCommand> validator,
        ILogger<ForgotPasswordHandler> logger
    )
    {
        _accessRepository = accessRepository;
        _tenantRepository = TenantRepository;
        _companyRepository = companyRepository;
        _tokenRepository = tokenRepository;
        _linkSender = linkSender;
        _throttle = throttle;
        _options = options;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        ForgotPasswordCommand command,
        CancellationToken cancellationToken
    )
    {
        var vr = await _validator.ValidateAsync(command, cancellationToken);
        if (!vr.IsValid)
            return Result<bool>.Failure(string.Join(" ", vr.Errors.Select(e => e.ErrorMessage)));

        LogPasswordResetRequested();

        var email = command.Email.Trim().ToLowerInvariant();

        // El cupo se consume antes de buscar la cuenta: cuenta inexistente y existente se
        // comportan igual frente al límite (sin side-channel).
        if (!await _throttle.TryAcquireAsync(email, cancellationToken))
            return Suppressed(PasswordResetSuppressionReason.RateLimited, userId: null);

        var identity = await _accessRepository.GetUserByEmailAsync(email, cancellationToken);
        if (identity is null)
            return Suppressed(PasswordResetSuppressionReason.NoAccount, userId: null);
        if (!identity.IsActive)
            return Suppressed(PasswordResetSuppressionReason.InactiveAccount, identity.Id);

        var memberships = await _accessRepository.GetActiveCompanyUserMembershipsForUserSystemAsync(
            identity.Id,
            cancellationToken
        );
        if (memberships.Count == 0)
            return Suppressed(PasswordResetSuppressionReason.NoActiveMembership, identity.Id);

        if (memberships.Count > 1)
        {
            var companyIds = memberships.Select(m => m.CompanyId).Distinct().ToList();
            var companies = await _companyRepository.GetByIdsAsync(companyIds, cancellationToken);
            // El token se liga a un tenant: con membresías en varios tenants el destino no es
            // inequívoco y no se elige uno arbitrario.
            if (companies.Select(c => c.TenantId).Distinct().Count() > 1)
                return Suppressed(PasswordResetSuppressionReason.AmbiguousTenant, identity.Id);
        }

        var companyList = await _companyRepository.GetByIdsAsync(
            new[] { memberships[0].CompanyId },
            cancellationToken
        );
        var company = companyList.Count > 0 ? companyList[0] : null;
        var tenantId = company?.TenantId ?? Guid.Empty;
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return Suppressed(PasswordResetSuppressionReason.InactiveTenant, identity.Id);

        // identity fue resuelto vía GetUserByEmailAsync — Email no puede ser null en este punto.
        await IssueTokenAndSendAsync(
            identity.Id,
            tenant.Id,
            PasswordResetToken.KindIdentity,
            identity.Email!.Value,
            cancellationToken
        );
        LogPasswordResetDeliveryRequested(identity.Id, tenant.Id);
        return Result<bool>.Success(true);
    }

    private Result<bool> Suppressed(PasswordResetSuppressionReason reason, Guid? userId)
    {
        LogPasswordResetRequestSuppressed(reason, userId);
        return Result<bool>.Success(true);
    }

    private async Task IssueTokenAndSendAsync(
        Guid userId,
        Guid? tenantId,
        string userKind,
        string email,
        CancellationToken cancellationToken
    )
    {
        var (raw, _) = await PasswordResetTokenIssuer.IssueAsync(
            _tokenRepository,
            _options,
            userId,
            userKind,
            tenantId,
            overrideLifetimeMinutes: null,
            cancellationToken
        );

        var link = BuildResetLink(_options.Value.PublicBaseUrl, raw, tenantId);
        await _linkSender.SendPasswordResetLinkAsync(email, link, cancellationToken);
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

    // Eventos seguros: nunca email, token, enlace ni hash del token.
    [LoggerMessage(
        EventId = 4101,
        EventName = "PasswordResetRequested",
        Level = LogLevel.Information,
        Message = "Solicitud de recuperación de contraseña recibida."
    )]
    private partial void LogPasswordResetRequested();

    [LoggerMessage(
        EventId = 4102,
        EventName = "PasswordResetRequestSuppressed",
        Level = LogLevel.Information,
        Message = "Solicitud de recuperación de contraseña suprimida: {Reason} (usuario {UserId})."
    )]
    private partial void LogPasswordResetRequestSuppressed(PasswordResetSuppressionReason reason, Guid? userId);

    [LoggerMessage(
        EventId = 4103,
        EventName = "PasswordResetDeliveryRequested",
        Level = LogLevel.Information,
        Message = "Entrega de recuperación de contraseña solicitada para usuario {UserId} (tenant {TenantId})."
    )]
    private partial void LogPasswordResetDeliveryRequested(Guid userId, Guid tenantId);
}

/// <summary>Motivo interno (solo logs) por el que no se emitió token; nunca se expone públicamente.</summary>
public enum PasswordResetSuppressionReason
{
    RateLimited = 1,
    NoAccount = 2,
    InactiveAccount = 3,
    NoActiveMembership = 4,
    AmbiguousTenant = 5,
    InactiveTenant = 6,
}
