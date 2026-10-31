using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicInvoicing.Services;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.GetElectronicInvoicingStatus;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using RequirementCodes = ERP.Application.Modules.ElectronicDocuments.Readiness.ElectronicEmissionRequirementCodes;
using Severity = ERP.Application.Modules.ElectronicDocuments.Readiness.ElectronicEmissionRequirementSeverity;

namespace ERP.Application.Modules.ElectronicDocuments.Readiness;

/// <summary>
/// Única implementación de <see cref="IElectronicEmissionRequirementsValidator"/>. Lee cada dato de
/// su fuente única — Company (RUC, razón social), Establishment matriz (dirMatriz), Establishment
/// del punto de emisión efectivo (dirEstablecimiento), SriSettings (ambiente, WSDL, certificado),
/// <see cref="ISriCertificateStatusResolver"/> (vigencia/contraseña) e
/// <see cref="ISriDocTypeCatalogResolver"/> — con las mismas reglas que exigen los builders XML y
/// los diagnósticos SRI existentes. Todo acceso filtra por tenant + empresa (fail-closed).
/// </summary>
public sealed class ElectronicEmissionRequirementsValidator
    : IElectronicEmissionRequirementsValidator
{
    /// <summary>Longitud exigida por los builders XML para el RUC del emisor.</summary>
    private const int RucLength = 13;

    private readonly ICompanyRepository _companyRepo;
    private readonly IEstablishmentRepository _establishmentRepo;
    private readonly IEmissionPointRepository _emissionPointRepo;
    private readonly ISriSettingsRepository _sriSettingsRepo;
    private readonly ISriCertificateStatusResolver _certificateStatusResolver;
    private readonly ISriDocTypeCatalogResolver _docTypeCatalogResolver;

    public ElectronicEmissionRequirementsValidator(
        ICompanyRepository companyRepo,
        IEstablishmentRepository establishmentRepo,
        IEmissionPointRepository emissionPointRepo,
        ISriSettingsRepository sriSettingsRepo,
        ISriCertificateStatusResolver certificateStatusResolver,
        ISriDocTypeCatalogResolver docTypeCatalogResolver
    )
    {
        _companyRepo = companyRepo;
        _establishmentRepo = establishmentRepo;
        _emissionPointRepo = emissionPointRepo;
        _sriSettingsRepo = sriSettingsRepo;
        _certificateStatusResolver = certificateStatusResolver;
        _docTypeCatalogResolver = docTypeCatalogResolver;
    }

    public async Task<ElectronicEmissionRequirementsResult> ValidateAsync(
        ElectronicEmissionRequirementsRequest request,
        CancellationToken ct = default
    )
    {
        var (tid, cid) = (request.TenantId, request.CompanyId);
        var issues = new List<ElectronicEmissionRequirementIssue>();
        void Add(string code, ReadinessActionTarget? target, Severity severity = Severity.Required) =>
            issues.Add(new(code, severity, target));

        // ── Identidad del emisor (Company) ──
        var company = tid == Guid.Empty || cid == Guid.Empty
            ? null
            : await _companyRepo.GetByIdForTenantAsync(cid, tid, ct);
        if (company is null)
        {
            // Sin empresa válida en el scope no se evalúa nada más: fail-closed, sin lecturas
            // adicionales con un scope inválido.
            Add(RequirementCodes.Company, ReadinessActionTarget.CompanyProfile);
            return new(issues);
        }
        if (company.TaxIdentificationNumber?.Trim().Length != RucLength)
            Add(RequirementCodes.IssuerTaxId, ReadinessActionTarget.CompanyProfile);
        if (string.IsNullOrWhiteSpace(company.LegalName))
            Add(RequirementCodes.IssuerLegalName, ReadinessActionTarget.CompanyProfile);

        // ── Dirección matriz (dirMatriz) ──
        var main = await _establishmentRepo.GetMainByCompanyAsync(tid, cid, ct);
        if (main is null)
            Add(RequirementCodes.MainEstablishment, ReadinessActionTarget.Establishments);
        else if (string.IsNullOrWhiteSpace(main.Address))
            Add(RequirementCodes.MatrixAddress, ReadinessActionTarget.Establishments);

        // ── Punto de emisión efectivo y su establecimiento (dirEstablecimiento) ──
        var emissionPoint = await _emissionPointRepo.GetByIdForCompanyAsync(
            tid,
            cid,
            request.EmissionPointId,
            ct
        );
        var establishment = emissionPoint is null
            ? null
            : await _establishmentRepo.GetByIdForCompanyAsync(
                tid,
                cid,
                emissionPoint.EstablishmentId,
                ct
            );
        if (emissionPoint is null || establishment is null)
            Add(RequirementCodes.EmissionPoint, ReadinessActionTarget.EmissionPoints);
        // Si es la matriz, su dirección ya se reportó como dirMatriz: un solo requisito por dato.
        else if (string.IsNullOrWhiteSpace(establishment.Address) && establishment.Id != main?.Id)
            Add(RequirementCodes.EstablishmentAddress, ReadinessActionTarget.Establishments);

        // ── Catálogo de tipos de comprobante ──
        if (!await _docTypeCatalogResolver.IsActiveElectronicDocTypeAsync(request.DocTypeCode, ct))
            Add(RequirementCodes.DocType, null);

        // ── SriSettings + certificado ──
        var settings = await _sriSettingsRepo.GetByCompanyIdAsync(cid, ct);
        if (settings is null || settings.TenantId != tid)
        {
            Add(RequirementCodes.SriSettings, ReadinessActionTarget.ElectronicInvoicingSettings);
            return new(issues);
        }
        if (settings.Environment
            is not (SriEnvironmentCodes.TestingValue or SriEnvironmentCodes.ProductionValue))
            Add(RequirementCodes.SriEnvironment, ReadinessActionTarget.ElectronicInvoicingSettings);
        if (!SriWsdlUrl.IsValidFor(settings.WsdlUrl, settings.Environment))
            Add(RequirementCodes.SriWsdlUrl, ReadinessActionTarget.ElectronicInvoicingSettings);

        var certificateIssue = await ResolveCertificateIssueAsync(settings, ct);
        if (certificateIssue is not null)
            issues.Add(certificateIssue);

        return new(issues);
    }

    /// <summary>
    /// Certificado: ausente, inutilizable (inaccesible / contraseña) y vencido bloquean; próximo a
    /// vencer (mismo umbral que el estado de facturación electrónica) solo advierte.
    /// </summary>
    private async Task<ElectronicEmissionRequirementIssue?> ResolveCertificateIssueAsync(
        SriSettings settings,
        CancellationToken ct
    )
    {
        const ReadinessActionTarget target = ReadinessActionTarget.ElectronicInvoicingSettings;
        if (string.IsNullOrWhiteSpace(settings.CertP12Path))
            return new(RequirementCodes.Certificate, Severity.Required, target);

        var status = await _certificateStatusResolver.ResolveAsync(settings, ct);
        if (!status.Installed || !status.PasswordCorrect || status.NotAfterUtc is not { } notAfter)
            return new(RequirementCodes.CertificateUnusable, Severity.Required, target);
        if (notAfter <= DateTime.UtcNow)
            return new(RequirementCodes.CertificateExpired, Severity.Required, target);
        if (!status.Valid)
            return new(RequirementCodes.CertificateUnusable, Severity.Required, target);

        var daysRemaining = (int)Math.Ceiling((notAfter - DateTime.UtcNow).TotalDays);
        return daysRemaining
            <= GetElectronicInvoicingStatusQueryHandler.CertificateExpiringThresholdDays
            ? new(RequirementCodes.CertificateExpiring, Severity.Warning, target)
            : null;
    }
}
