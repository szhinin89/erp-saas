using MediatR;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Application.Modules.ElectronicInvoicing.Enums;
using ERP.Application.Modules.ElectronicInvoicing.Services;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.GetElectronicInvoicingStatus;

/// <summary>
/// Única fuente de verdad del estado operativo de facturación electrónica — ver
/// <see cref="ElectronicInvoicingStatus"/>. El frontend (<c>ZHElectronicEnvironmentBanner</c>)
/// nunca recalcula este árbol de decisión: solo renderiza <see cref="ElectronicInvoicingStatusDto.Status"/>.
/// </summary>
public sealed partial class GetElectronicInvoicingStatusQueryHandler
    : IRequestHandler<GetElectronicInvoicingStatusQuery, Result<ElectronicInvoicingStatusDto>>
{
    /// <summary>Días de antelación para avisar vencimiento próximo del certificado.</summary>
    public const int CertificateExpiringThresholdDays = 30;

    private readonly ISriSettingsRepository _repo;
    private readonly ICurrentCompany _currentCompany;
    private readonly ISriCertificateStatusResolver _certStatusResolver;
    private readonly ISriConnectivityChecker _connectivityChecker;
    private readonly ILogger<GetElectronicInvoicingStatusQueryHandler> _logger;

    public GetElectronicInvoicingStatusQueryHandler(
        ISriSettingsRepository repo,
        ICurrentCompany currentCompany,
        ISriCertificateStatusResolver certStatusResolver,
        ISriConnectivityChecker connectivityChecker,
        ILogger<GetElectronicInvoicingStatusQueryHandler> logger)
    {
        _repo               = repo;
        _currentCompany     = currentCompany;
        _certStatusResolver = certStatusResolver;
        _connectivityChecker = connectivityChecker;
        _logger             = logger;
    }

    public async Task<Result<ElectronicInvoicingStatusDto>> Handle(
        GetElectronicInvoicingStatusQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _repo.GetByCompanyIdAsync(_currentCompany.CompanyId, cancellationToken);
            if (settings is null)
                return Result<ElectronicInvoicingStatusDto>.Success(NotConfiguredDto());

            return Result<ElectronicInvoicingStatusDto>.Success(await ResolveConfiguredStatusAsync(settings, cancellationToken));
        }
        catch (Exception ex)
        {
            LogStatusResolutionFailed(ex);
            return Result<ElectronicInvoicingStatusDto>.Success(ErrorDto());
        }
    }

    private async Task<ElectronicInvoicingStatusDto> ResolveConfiguredStatusAsync(SriSettings settings, CancellationToken cancellationToken)
    {
        // Códigos oficiales SRI (Ficha Técnica, Tabla 4 "Ambiente"): 1=Pruebas, 2=Producción.
        var (environment, environmentName) = settings.Environment switch
        {
            1 => ("Test", "Pruebas"),
            2 => ("Production", "Producción"),
            _ => ((string?)null, (string?)null),
        };
        var emissionType = settings.EmissionType == 1 ? "Normal" : null;

        var certStatus = await _certStatusResolver.ResolveAsync(settings, cancellationToken);
        var certificateOk = certStatus.Installed && certStatus.PasswordCorrect;

        if (!certificateOk)
        {
            return new ElectronicInvoicingStatusDto(
                Status: ElectronicInvoicingStatus.Incomplete,
                Configured: true, environment, environmentName, emissionType,
                CertificateInstalled: certStatus.Installed, CertificateValid: false,
                CertificateExpiresAt: null, CertificateDaysRemaining: null,
                SriAvailability: SriAvailability.Unknown, CanIssue: false);
        }

        var daysRemaining = certStatus.NotAfterUtc.HasValue
            ? (int)Math.Ceiling((certStatus.NotAfterUtc.Value - DateTime.UtcNow).TotalDays)
            : (int?)null;

        if (daysRemaining.HasValue && daysRemaining.Value <= 0)
        {
            return new ElectronicInvoicingStatusDto(
                Status: ElectronicInvoicingStatus.CertificateExpired,
                Configured: true, environment, environmentName, emissionType,
                CertificateInstalled: true, CertificateValid: false,
                CertificateExpiresAt: certStatus.NotAfterUtc, CertificateDaysRemaining: daysRemaining,
                SriAvailability: SriAvailability.Unknown, CanIssue: false);
        }

        if (daysRemaining.HasValue && daysRemaining.Value <= CertificateExpiringThresholdDays)
        {
            return new ElectronicInvoicingStatusDto(
                Status: ElectronicInvoicingStatus.CertificateExpiring,
                Configured: true, environment, environmentName, emissionType,
                CertificateInstalled: true, CertificateValid: true,
                CertificateExpiresAt: certStatus.NotAfterUtc, CertificateDaysRemaining: daysRemaining,
                SriAvailability: SriAvailability.Unknown, CanIssue: true);
        }

        var urlValid = Uri.TryCreate(settings.WsdlUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        if (!urlValid)
        {
            return new ElectronicInvoicingStatusDto(
                Status: ElectronicInvoicingStatus.Incomplete,
                Configured: true, environment, environmentName, emissionType,
                CertificateInstalled: true, CertificateValid: true,
                CertificateExpiresAt: certStatus.NotAfterUtc, CertificateDaysRemaining: daysRemaining,
                SriAvailability: SriAvailability.Unknown, CanIssue: false);
        }

        // Solo se comprueba conectividad SRI cuando el resto ya es válido — evita una llamada
        // de red desperdiciada cuando el estado ya está determinado por otra causa.
        var reachable = await _connectivityChecker.PingAsync(settings.WsdlUrl, cancellationToken);
        if (!reachable)
        {
            return new ElectronicInvoicingStatusDto(
                Status: ElectronicInvoicingStatus.SriUnavailable,
                Configured: true, environment, environmentName, emissionType,
                CertificateInstalled: true, CertificateValid: true,
                CertificateExpiresAt: certStatus.NotAfterUtc, CertificateDaysRemaining: daysRemaining,
                SriAvailability: SriAvailability.Unavailable, CanIssue: false);
        }

        var status = environment == "Production" ? ElectronicInvoicingStatus.Ready : ElectronicInvoicingStatus.Testing;
        return new ElectronicInvoicingStatusDto(
            Status: status,
            Configured: true, environment, environmentName, emissionType,
            CertificateInstalled: true, CertificateValid: true,
            CertificateExpiresAt: certStatus.NotAfterUtc, CertificateDaysRemaining: daysRemaining,
            SriAvailability: SriAvailability.Available, CanIssue: true);
    }

    private static ElectronicInvoicingStatusDto NotConfiguredDto() => new(
        Status: ElectronicInvoicingStatus.NotConfigured,
        Configured: false, Environment: null, EnvironmentName: null, EmissionType: null,
        CertificateInstalled: false, CertificateValid: false,
        CertificateExpiresAt: null, CertificateDaysRemaining: null,
        SriAvailability: SriAvailability.Unknown, CanIssue: false);

    private static ElectronicInvoicingStatusDto ErrorDto() => new(
        Status: ElectronicInvoicingStatus.Error,
        Configured: false, Environment: null, EnvironmentName: null, EmissionType: null,
        CertificateInstalled: false, CertificateValid: false,
        CertificateExpiresAt: null, CertificateDaysRemaining: null,
        SriAvailability: SriAvailability.Unknown, CanIssue: false);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "[ElectronicInvoicing] No fue posible determinar el estado de facturación electrónica")]
    private partial void LogStatusResolutionFailed(Exception ex);
}
