using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.Readiness;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using PhysicalCodes = ERP.Application.Modules.Company.PhysicalEmission.PhysicalEmissionRequirementCodes;
using Severity = ERP.Application.Modules.ElectronicDocuments.Readiness.ElectronicEmissionRequirementSeverity;

namespace ERP.Application.Modules.Company.PhysicalEmission;

/// <summary>
/// Única implementación de <see cref="IPhysicalEmissionRequirementsValidator"/>. Cada dato sale de
/// su fuente única: Company (RUC, razón social, régimen), EmissionPoint, catálogo SRI
/// (<see cref="ISriDocTypeCatalogResolver"/>), <c>PhysicalDocumentAuthorization</c> y la secuencia
/// documental (solo lectura del próximo número; la captura sigue siendo exclusiva de
/// <c>CaptureNextAsync</c>). El día de vigencia es el día de la empresa (<see cref="ICompanyClock"/>).
/// </summary>
public sealed class PhysicalEmissionRequirementsValidator : IPhysicalEmissionRequirementsValidator
{
    /// <summary>Longitud del RUC (art. 18 num. 2: RUC del emisor preimpreso).</summary>
    private const int RucLength = 13;

    private readonly ICompanyRepository _companyRepo;
    private readonly IEmissionPointRepository _emissionPointRepo;
    private readonly ISriDocTypeCatalogResolver _docTypeCatalogResolver;
    private readonly IPhysicalDocumentAuthorizationRepository _authorizationRepo;
    private readonly IDocumentSequenceRepository _sequenceRepo;
    private readonly ICompanyClock _companyClock;

    public PhysicalEmissionRequirementsValidator(
        ICompanyRepository companyRepo,
        IEmissionPointRepository emissionPointRepo,
        ISriDocTypeCatalogResolver docTypeCatalogResolver,
        IPhysicalDocumentAuthorizationRepository authorizationRepo,
        IDocumentSequenceRepository sequenceRepo,
        ICompanyClock companyClock
    )
    {
        _companyRepo = companyRepo;
        _emissionPointRepo = emissionPointRepo;
        _docTypeCatalogResolver = docTypeCatalogResolver;
        _authorizationRepo = authorizationRepo;
        _sequenceRepo = sequenceRepo;
        _companyClock = companyClock;
    }

    public async Task<ElectronicEmissionRequirementsResult> ValidateAsync(
        PhysicalEmissionRequirementsRequest request,
        CancellationToken ct = default
    )
    {
        var (tid, cid) = (request.TenantId, request.CompanyId);
        var docTypeCode = request.DocTypeCode.Trim();
        var issues = new List<ElectronicEmissionRequirementIssue>();
        void Add(string code, ReadinessActionTarget? target) =>
            issues.Add(new(code, Severity.Required, target));
        ElectronicEmissionRequirementsResult Result() =>
            new(issues) { Modality = EmissionType.Physical };

        // ── Identidad del emisor (F-3) ──
        var company = tid == Guid.Empty || cid == Guid.Empty
            ? null
            : await _companyRepo.GetByIdForTenantAsync(cid, tid, ct);
        if (company is null)
        {
            // Sin empresa válida en el scope no se evalúa nada más (fail-closed).
            Add(PhysicalCodes.IssuerTaxId, ReadinessActionTarget.CompanyProfile);
            return Result();
        }
        if (
            company.IsTemporaryTaxIdentification
            || company.TaxIdentificationNumber?.Trim().Length != RucLength
        )
            Add(PhysicalCodes.IssuerTaxId, ReadinessActionTarget.CompanyProfile);
        if (string.IsNullOrWhiteSpace(company.LegalName))
            Add(PhysicalCodes.IssuerLegalName, ReadinessActionTarget.CompanyProfile);

        // ── Punto de emisión activo de la empresa ──
        // La modalidad la decide quien invoca (tipo del punto en la apertura, snapshot del borrador
        // al autorizar, POS-EMISSION-TYPE-SNAPSHOT-01); aquí se exige el punto y su autorización.
        var emissionPoint = await _emissionPointRepo.GetByIdForCompanyAsync(
            tid,
            cid,
            request.EmissionPointId,
            ct
        );
        if (emissionPoint is not { IsActive: true })
        {
            Add(PhysicalCodes.EmissionPoint, ReadinessActionTarget.EmissionPoints);
            return Result();
        }

        // ── Tipo habilitado y régimen que lo permite en físico (F-4, F-5) ──
        if (!await _docTypeCatalogResolver.IsActiveDocTypeAsync(docTypeCode, ct))
            Add(PhysicalCodes.DocType, null);
        if (string.IsNullOrWhiteSpace(company.TaxRegimeCode))
            Add(PhysicalCodes.TaxRegime, ReadinessActionTarget.CompanyFiscalSettings);
        else if (
            !await _docTypeCatalogResolver.IsPhysicalDocTypePermittedAsync(
                company.TaxRegimeCode,
                docTypeCode,
                ct
            )
        )
            Add(PhysicalCodes.DocTypeNotPermitted, ReadinessActionTarget.EmissionPoints);

        // ── Autorización de impresión, vigencia y rango (F-1) ──
        var authorizationIssue = await ResolveAuthorizationIssueAsync(
            request with
            {
                DocTypeCode = docTypeCode,
            },
            ct
        );
        if (authorizationIssue is not null)
            Add(authorizationIssue, ReadinessActionTarget.PhysicalDocumentAuthorizations);

        return Result();
    }

    /// <summary>
    /// La autorización que cubre el siguiente secuencial debe estar en vigor hoy (día de empresa).
    /// Sin ninguna autorización activa ⇒ <see cref="PhysicalCodes.Authorization"/>; con autorizaciones pero
    /// ninguna que cubra el siguiente número ⇒ <see cref="PhysicalCodes.SequentialOutOfRange"/>.
    /// </summary>
    private async Task<string?> ResolveAuthorizationIssueAsync(
        PhysicalEmissionRequirementsRequest request,
        CancellationToken ct
    )
    {
        var (tid, cid) = (request.TenantId, request.CompanyId);
        var authorizations = await _authorizationRepo.GetActiveAsync(
            tid,
            cid,
            request.EmissionPointId,
            request.DocTypeCode,
            ct
        );
        if (authorizations.Count == 0)
            return PhysicalCodes.Authorization;

        var sequence = await _sequenceRepo.GetByEmissionPointAndDocTypeAsync(
            request.EmissionPointId,
            request.DocTypeCode,
            ct
        );
        // Sin fila, CaptureNextAsync entrega 1. Solo lectura del próximo número.
        var nextSequential =
            sequence is not null && sequence.TenantId == tid && sequence.CompanyId == cid
                ? sequence.CurrentSeq
                : 1;

        var covering = authorizations.FirstOrDefault(a => a.Covers(nextSequential));
        if (covering is null)
            return PhysicalCodes.SequentialOutOfRange;

        var today = await _companyClock.TodayAsync(cid, tid, ct);
        if (covering.IsInForceOn(today))
            return null;
        return covering.ExpiresOn < today ? PhysicalCodes.AuthorizationExpired : PhysicalCodes.Authorization;
    }

    public async Task<IReadOnlyList<AuthorizedSequentialRange>> GetAuthorizedSequentialRangesAsync(
        PhysicalEmissionRequirementsRequest request,
        CancellationToken ct = default
    )
    {
        var (tid, cid) = (request.TenantId, request.CompanyId);
        var authorizations = await _authorizationRepo.GetActiveAsync(
            tid,
            cid,
            request.EmissionPointId,
            request.DocTypeCode.Trim(),
            ct
        );
        if (authorizations.Count == 0)
            return [];

        var today = await _companyClock.TodayAsync(cid, tid, ct);
        return authorizations
            .Where(a => a.IsInForceOn(today))
            .Select(a => new AuthorizedSequentialRange(a.FirstSequential, a.LastSequential))
            .ToList();
    }
}
