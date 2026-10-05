using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Application.Modules.Purchases.Services;

public sealed record RetentionCodeInfo(
    string TaxType,
    string Code,
    string Name,
    decimal Percentage,
    Guid? Id = null,
    bool IsActive = true
);

/// <summary>
/// ZH-SRI-RETENTION-CATALOG-SSOT-01 (ADR-037 D7) — motivo tipado por el que un concepto de retención no
/// pudo resolverse a su representación oficial. Nunca se infiere semántica desde mensajes de texto.
/// </summary>
public enum RetentionCodeResolutionError
{
    /// <summary>El concepto (tipo de impuesto + código, o Id) no existe en el catálogo global.</summary>
    ConceptNotFound = 1,

    /// <summary>Ninguna versión del concepto está vigente a la fecha del documento.</summary>
    NoValidVersion = 2,

    /// <summary>Más de una versión vigente a la misma fecha: catálogo ambiguo/corrupto.</summary>
    AmbiguousVersions = 3,

    /// <summary>La versión vigente no tiene código XML oficial (p. ej. 728 / IVA 15 %): no emitible.</summary>
    MissingXmlCode = 4,

    /// <summary>La tasa aplicada en el documento no coincide con el porcentaje oficial de la versión vigente.</summary>
    RateMismatch = 5,

    /// <summary>
    /// ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — la fuente oficial no fija una tarifa única ("12 o 14",
    /// "1 /0 según resolución…"): el ERP no puede determinar legalmente la tasa aplicable.
    /// </summary>
    ConditionalRateUndetermined = 6,

    /// <summary>La versión vigente no tiene una fuente normativa registrada: sin evidencia no se emite.</summary>
    NormativeEvidenceMissing = 7,
}

/// <summary>Representación oficial vigente de un concepto de retención a una fecha de documento.</summary>
public sealed record RetentionCodeRepresentation(
    Guid RetentionCodeId,
    Guid VersionId,
    string TaxType,
    string Code,
    string XmlCode,
    decimal? Percentage,
    Guid NormativeSourceId,
    string NormativeDocument,
    string? NormativeVersion,
    string? NormativeSection
);

/// <summary>Resultado tipado y fail-closed de <see cref="IRetentionCodeResolver.ResolveForDateAsync(RetentionTaxType, string, DateOnly, decimal, CancellationToken)"/>.</summary>
public sealed record RetentionCodeResolution(
    RetentionCodeRepresentation? Representation,
    RetentionCodeResolutionError? Error,
    string? Detail
)
{
    public bool IsResolved => Representation is not null && Error is null;

    public static RetentionCodeResolution Resolved(RetentionCodeRepresentation representation) =>
        new(representation, null, null);

    public static RetentionCodeResolution Failed(
        RetentionCodeResolutionError error,
        string detail
    ) => new(null, error, detail);
}

/// <summary>
/// Único resolver del catálogo global de retenciones (<c>global.sri_retention_code</c> + versiones). Contrato
/// de lectura de ADR-037 D13 — los módulos funcionales nunca filtran <c>IsActive</c> por su cuenta:
/// <list type="bullet">
/// <item><c>GetSelectable…</c>: habilitación operativa (<c>IsActive</c>) para operaciones/configuración nuevas.</item>
/// <item><c>GetByIdIncludingDisabledAsync</c>: lectura histórica por Id, sin filtrar <c>IsActive</c>.</item>
/// <item><c>ResolveForDateAsync</c>: representación fiscal vigente a la fecha del documento, sin filtrar
/// <c>IsActive</c> (un concepto deshabilitado después sigue resolviendo sus documentos).</item>
/// </list>
/// </summary>
public interface IRetentionCodeResolver
{
    /// <summary>Concepto seleccionable por clave de negocio (habilitado). Null si no existe o no está habilitado.</summary>
    Task<RetentionCodeInfo?> GetSelectableByCodeAsync(
        string code,
        string taxType,
        CancellationToken ct = default
    );

    /// <summary>
    /// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01: concepto seleccionable por Id de catálogo (FK real desde
    /// SupplierRetentionDefault.SriRetentionCodeId) para documentos nuevos — null si no existe o no está
    /// habilitado; nunca lanza excepción por dato huérfano.
    /// </summary>
    Task<RetentionCodeInfo?> GetSelectableByIdAsync(
        Guid sriRetentionCodeId,
        CancellationToken ct = default
    );

    /// <summary>Lectura histórica por Id: devuelve el concepto aunque ya no esté habilitado (<see cref="RetentionCodeInfo.IsActive"/>).</summary>
    Task<RetentionCodeInfo?> GetByIdIncludingDisabledAsync(
        Guid sriRetentionCodeId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Representación oficial vigente a <paramref name="documentIssueDate"/> del concepto con Id
    /// <paramref name="retentionCodeId"/>. Fail-closed: 0 o más de 1 versión vigente, código XML vacío o
    /// <paramref name="appliedRate"/> distinta del porcentaje oficial (cuando la versión lo fija) → error tipado.
    /// </summary>
    Task<RetentionCodeResolution> ResolveForDateAsync(
        Guid retentionCodeId,
        DateOnly documentIssueDate,
        decimal appliedRate,
        CancellationToken ct = default
    );

    /// <summary>
    /// Igual que <see cref="ResolveForDateAsync(Guid, DateOnly, decimal, CancellationToken)"/>, identificando el
    /// concepto por tipo de impuesto + clave de negocio (único, <c>uq_sri_ret_code</c>) — la forma en que
    /// <c>RetentionDocumentLine</c> conserva su snapshot.
    /// </summary>
    Task<RetentionCodeResolution> ResolveForDateAsync(
        RetentionTaxType taxType,
        string code,
        DateOnly documentIssueDate,
        decimal appliedRate,
        CancellationToken ct = default
    );
}
