using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace ERP.Application.Auth.UseCases.PasswordReset;

public sealed class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, Result<bool>>
{
    public const string NoAccountMessage = "No existe una cuenta con ese correo.";
    public const string MultipleAccountsMessage = "Hay múltiples cuentas con ese correo. Contacte a soporte.";

    private readonly IAccessRepository _accessRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IPasswordResetLinkSender _linkSender;
    private readonly IOptions<PasswordResetOptions> _options;
    private readonly IValidator<ForgotPasswordCommand> _validator;

    public ForgotPasswordHandler(
        IAccessRepository accessRepository,
        ITenantRepository TenantRepository,
        ICompanyRepository companyRepository,
        IPasswordResetTokenRepository tokenRepository,
        IPasswordResetLinkSender linkSender,
        IOptions<PasswordResetOptions> options,
        IValidator<ForgotPasswordCommand> validator)
    {
        _accessRepository = accessRepository;
        _tenantRepository = TenantRepository;
        _companyRepository = companyRepository;
        _tokenRepository = tokenRepository;
        _linkSender = linkSender;
        _options = options;
        _validator = validator;
    }

    public async Task<Result<bool>> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var vr = await _validator.ValidateAsync(command, cancellationToken);
        if (!vr.IsValid)
            return Result<bool>.Failure(string.Join(" ", vr.Errors.Select(e => e.ErrorMessage)));

        var email = command.Email.Trim().ToLowerInvariant();
        var identity = await _accessRepository.GetUserByEmailAsync(email, cancellationToken);
        if (identity is null || !identity.IsActive)
            return Result<bool>.Failure(NoAccountMessage);

        var memberships = await _accessRepository.GetActiveCompanyUserMembershipsForUserSystemAsync(identity.Id, cancellationToken);
        if (memberships.Count == 0)
            return Result<bool>.Failure(NoAccountMessage);

        if (memberships.Count > 1)
        {
            var companyIds = memberships.Select(m => m.CompanyId).Distinct().ToList();
            var companies = await _companyRepository.GetByIdsAsync(companyIds, cancellationToken);
            if (companies.Select(c => c.TenantId).Distinct().Count() > 1)
                return Result<bool>.Failure(MultipleAccountsMessage);
        }

        var companyList = await _companyRepository.GetByIdsAsync(new[] { memberships[0].CompanyId }, cancellationToken);
        var company = companyList.Count > 0 ? companyList[0] : null;
        var tenantId = company?.TenantId ?? Guid.Empty;
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive)
            return Result<bool>.Failure(NoAccountMessage);

        // identity fue resuelto vía GetUserByEmailAsync — Email no puede ser null en este punto.
        await IssueTokenAndSendAsync(identity.Id, tenant.Id, PasswordResetToken.KindIdentity, identity.Email!.Value, cancellationToken);
        return Result<bool>.Success(true);
    }

    private async Task IssueTokenAndSendAsync(
        Guid userId, Guid? tenantId, string userKind, string email, CancellationToken cancellationToken)
    {
        var (raw, _) = await PasswordResetTokenIssuer.IssueAsync(
            _tokenRepository, _options, userId, userKind, tenantId,
            overrideLifetimeMinutes: null, cancellationToken);

        var link = BuildResetLink(_options.Value.PublicBaseUrl, raw, tenantId);
        await _linkSender.SendPasswordResetLinkAsync(email, link, cancellationToken);
    }

    private static string BuildResetLink(string publicBaseUrl, string rawToken, Guid? tenantId)
    {
        var baseUrl = (publicBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl)) baseUrl = "http://localhost:5173";

        var tokenEnc = Uri.EscapeDataString(rawToken);
        var qs = $"token={tokenEnc}";
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            qs += $"&tenantId={tenantId.Value}";

        return $"{baseUrl}/reset-password?{qs}";
    }
}
