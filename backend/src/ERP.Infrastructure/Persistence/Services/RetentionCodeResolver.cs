using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.SriCatalogs.Entities;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ERP.Infrastructure.Persistence.Services;

/// <summary>
/// Implementación única de <see cref="IRetentionCodeResolver"/> sobre el catálogo global
/// <c>global.sri_retention_code</c> + <c>global.sri_retention_code_version</c> (ADR-037). Catálogos globales:
/// sin filtros de tenant/empresa y sin <c>HasQueryFilter</c> por habilitación.
/// </summary>
public sealed class RetentionCodeResolver : IRetentionCodeResolver
{
    private readonly ErpDbContext _db;

    public RetentionCodeResolver(ErpDbContext db) => _db = db;

    public async Task<RetentionCodeInfo?> GetSelectableByCodeAsync(
        string code,
        string taxType,
        CancellationToken ct = default
    )
    {
        var r = await _db
            .SriRetentionCodes.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Code == code && x.TaxType == taxType.ToUpperInvariant() && x.IsActive,
                ct
            );

        return r is null ? null : ToInfo(r);
    }

    public async Task<RetentionCodeInfo?> GetSelectableByIdAsync(
        Guid sriRetentionCodeId,
        CancellationToken ct = default
    )
    {
        var r = await _db
            .SriRetentionCodes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sriRetentionCodeId && x.IsActive, ct);

        return r is null ? null : ToInfo(r);
    }

    public async Task<RetentionCodeInfo?> GetByIdIncludingDisabledAsync(
        Guid sriRetentionCodeId,
        CancellationToken ct = default
    )
    {
        var r = await _db
            .SriRetentionCodes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sriRetentionCodeId, ct);

        return r is null ? null : ToInfo(r);
    }

    public async Task<RetentionCodeResolution> ResolveForDateAsync(
        Guid retentionCodeId,
        DateOnly documentIssueDate,
        decimal appliedRate,
        CancellationToken ct = default
    )
    {
        // Deliberadamente sin filtrar IsActive: la habilitación operativa no afecta la resolución
        // fiscal de un documento existente (ADR-037 D13).
        var concept = await _db
            .SriRetentionCodes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == retentionCodeId, ct);

        return concept is null
            ? RetentionCodeResolution.Failed(
                RetentionCodeResolutionError.ConceptNotFound,
                $"El código de retención con Id '{retentionCodeId}' no existe en el catálogo SRI."
            )
            : await ResolveConceptAsync(concept, documentIssueDate, appliedRate, ct);
    }

    public async Task<RetentionCodeResolution> ResolveForDateAsync(
        RetentionTaxType taxType,
        string code,
        DateOnly documentIssueDate,
        decimal appliedRate,
        CancellationToken ct = default
    )
    {
        var catalogTaxType = ToCatalogTaxType(taxType);
        var normalizedCode = code.Trim();
        var concept = catalogTaxType is null
            ? null
            : await _db
                .SriRetentionCodes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TaxType == catalogTaxType && x.Code == normalizedCode, ct);

        return concept is null
            ? RetentionCodeResolution.Failed(
                RetentionCodeResolutionError.ConceptNotFound,
                $"El código de retención '{normalizedCode}' ({taxType}) no existe en el catálogo SRI."
            )
            : await ResolveConceptAsync(concept, documentIssueDate, appliedRate, ct);
    }

    private async Task<RetentionCodeResolution> ResolveConceptAsync(
        SriRetentionCode concept,
        DateOnly date,
        decimal appliedRate,
        CancellationToken ct
    )
    {
        var versions = await _db
            .SriRetentionCodeVersions.AsNoTracking()
            .Where(v =>
                v.RetentionCodeId == concept.Id
                && (v.ValidFrom == null || v.ValidFrom <= date)
                && (v.ValidUntil == null || v.ValidUntil >= date)
            )
            .ToListAsync(ct);

        var label = $"'{concept.Code}' ({concept.TaxType})";
        if (versions.Count == 0)
            return RetentionCodeResolution.Failed(
                RetentionCodeResolutionError.NoValidVersion,
                $"El código de retención {label} no tiene una representación SRI vigente al {FormatDate(date)}."
            );
        if (versions.Count > 1)
            return RetentionCodeResolution.Failed(
                RetentionCodeResolutionError.AmbiguousVersions,
                $"El código de retención {label} tiene {versions.Count} representaciones SRI vigentes al {FormatDate(date)}; el catálogo es ambiguo."
            );

        var version = versions[0];
        if (string.IsNullOrWhiteSpace(version.XmlCode))
            return RetentionCodeResolution.Failed(
                RetentionCodeResolutionError.MissingXmlCode,
                $"El código de retención {label} no tiene código oficial SRI para el comprobante electrónico; no puede emitirse."
            );
        if (version.Percentage is { } officialRate && officialRate != appliedRate)
            return RetentionCodeResolution.Failed(
                RetentionCodeResolutionError.RateMismatch,
                $"El código de retención {label} corresponde al {FormatRate(officialRate)}% y la línea aplica {FormatRate(appliedRate)}%."
            );

        var source = await _db
            .SriNormativeSources.AsNoTracking()
            .FirstAsync(s => s.Id == version.NormativeSourceId, ct);

        return RetentionCodeResolution.Resolved(
            new RetentionCodeRepresentation(
                concept.Id,
                version.Id,
                concept.TaxType,
                concept.Code,
                version.XmlCode,
                version.Percentage,
                source.Id,
                source.Document,
                source.Version,
                source.Section
            )
        );
    }

    /// <summary>Clave de tipo de impuesto del catálogo global para el tipo interno de la línea.</summary>
    private static string? ToCatalogTaxType(RetentionTaxType taxType) =>
        taxType switch
        {
            RetentionTaxType.Vat => "IVA",
            RetentionTaxType.Income => "RENTA",
            _ => null,
        };

    private static string FormatDate(DateOnly date) =>
        date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string FormatRate(decimal rate) => rate.ToString("0.####", CultureInfo.InvariantCulture);

    private static RetentionCodeInfo ToInfo(SriRetentionCode r) =>
        new(r.TaxType, r.Code, r.Name, r.Percentage, r.Id, r.IsActive);
}
