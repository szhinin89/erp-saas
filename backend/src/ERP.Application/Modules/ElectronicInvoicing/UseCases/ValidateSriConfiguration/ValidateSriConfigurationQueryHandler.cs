using ERP.Application.Common;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.ElectronicInvoicing.DTOs;
using ERP.Application.Modules.ElectronicInvoicing.Services;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;
using System.Globalization;

namespace ERP.Application.Modules.ElectronicInvoicing.UseCases.ValidateSriConfiguration;

public sealed class ValidateSriConfigurationQueryHandler
    : IRequestHandler<ValidateSriConfigurationQuery, Result<SriConfigurationValidationDto>>
{
    private readonly ISriSettingsRepository _sriSettingsRepo;
    private readonly ICompanyRepository _companyRepo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ISriCertificateStatusResolver _certStatusResolver;
    private readonly ISriConnectivityChecker _connectivityChecker;

    public ValidateSriConfigurationQueryHandler(
        ISriSettingsRepository sriSettingsRepo,
        ICompanyRepository companyRepo,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ISriCertificateStatusResolver certStatusResolver,
        ISriConnectivityChecker connectivityChecker
    )
    {
        _sriSettingsRepo = sriSettingsRepo;
        _companyRepo = companyRepo;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _certStatusResolver = certStatusResolver;
        _connectivityChecker = connectivityChecker;
    }

    public async Task<Result<SriConfigurationValidationDto>> Handle(
        ValidateSriConfigurationQuery request,
        CancellationToken cancellationToken
    )
    {
        var checks = new List<SriConfigurationCheckDto>();
        SriCertificateInfoDto? certificateInfo = null;
        var companyId = _currentCompany.CompanyId;

        var settings = await _sriSettingsRepo.GetByCompanyIdAsync(companyId, cancellationToken);
        if (settings is null)
        {
            checks.Add(
                new SriConfigurationCheckDto(
                    "configuration",
                    false,
                    "No existe configuración SRI registrada para esta empresa."
                )
            );
            return Result<SriConfigurationValidationDto>.Success(
                new SriConfigurationValidationDto(false, checks, certificateInfo)
            );
        }
        checks.Add(
            new SriConfigurationCheckDto(
                "configuration",
                true,
                "Existe configuración SRI registrada."
            )
        );

        var environmentValid = settings.Environment is 1 or 2;
        checks.Add(
            new SriConfigurationCheckDto(
                "environment",
                environmentValid,
                environmentValid
                    ? "El ambiente configurado es válido."
                    : "El ambiente configurado no es válido."
            )
        );

        var certStatus = await _certStatusResolver.ResolveAsync(settings, cancellationToken);

        checks.Add(
            new SriConfigurationCheckDto(
                "certificateFile",
                certStatus.Installed,
                certStatus.Installed
                    ? "El certificado se encontró en el almacenamiento."
                    : "No se encontró ningún certificado cargado para esta empresa."
            )
        );

        if (certStatus.Installed && !string.IsNullOrWhiteSpace(settings.CertPassword))
        {
            checks.Add(
                new SriConfigurationCheckDto(
                    "certificatePassword",
                    certStatus.PasswordCorrect,
                    certStatus.PasswordCorrect
                        ? "La contraseña del certificado es correcta."
                        : $"No se pudo abrir el certificado con la contraseña configurada: {certStatus.ErrorMessage}"
                )
            );

            int? daysRemaining = null;
            if (certStatus.PasswordCorrect && certStatus.NotAfterUtc.HasValue)
            {
                daysRemaining = (int)
                    Math.Ceiling((certStatus.NotAfterUtc.Value - DateTime.UtcNow).TotalDays);
                var notExpired = certStatus.NotAfterUtc.Value > DateTime.UtcNow;
                checks.Add(
                    new SriConfigurationCheckDto(
                        "certificateExpiry",
                        notExpired,
                        notExpired
                            ? $"El certificado es válido hasta {certStatus.NotAfterUtc.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}."
                            : $"El certificado está vencido desde {certStatus.NotAfterUtc.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}."
                    )
                );

                var company = await _companyRepo.GetByIdForTenantAsync(
                    companyId,
                    _currentTenant.TenantId,
                    cancellationToken
                );
                if (company is not null && !string.IsNullOrWhiteSpace(certStatus.Subject))
                {
                    var rucDigits = new string(
                        company.TaxIdentificationNumber.Where(char.IsDigit).ToArray()
                    );
                    var cedulaPrefix = rucDigits.Length >= 10 ? rucDigits[..10] : rucDigits;
                    var subjectMatches =
                        cedulaPrefix.Length > 0
                        && certStatus.Subject.Contains(cedulaPrefix, StringComparison.Ordinal);

                    checks.Add(
                        new SriConfigurationCheckDto(
                            "certificateOwner",
                            subjectMatches,
                            subjectMatches
                                ? "El certificado corresponde al RUC de la empresa."
                                : "No se pudo confirmar que el certificado pertenece al RUC de la empresa "
                                    + "(verificación best-effort: depende del formato del certificado emitido)."
                        )
                    );
                }
            }

            certificateInfo = new SriCertificateInfoDto(
                certStatus.PasswordCorrect,
                certStatus.PasswordCorrect,
                certStatus.NotAfterUtc,
                daysRemaining,
                certStatus.Subject,
                certStatus.Issuer,
                certStatus.ErrorMessage
            );
        }

        var urlValid =
            Uri.TryCreate(settings.WsdlUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        checks.Add(
            new SriConfigurationCheckDto(
                "wsdlUrl",
                urlValid,
                urlValid
                    ? "La URL del webservice SRI tiene un formato válido."
                    : "La URL del webservice SRI no es válida."
            )
        );

        if (urlValid)
        {
            var reachable = await _connectivityChecker.PingAsync(
                settings.WsdlUrl,
                cancellationToken
            );
            checks.Add(
                new SriConfigurationCheckDto(
                    "wsdlReachable",
                    reachable,
                    reachable
                        ? "El webservice del SRI respondió correctamente."
                        : "No se pudo conectar al webservice del SRI."
                )
            );
        }

        var isValid = checks.All(c => c.Passed);
        return Result<SriConfigurationValidationDto>.Success(
            new SriConfigurationValidationDto(isValid, checks, certificateInfo)
        );
    }
}
