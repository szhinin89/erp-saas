using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.AdditionalInfo;

/// <summary>
/// ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D6) — datos con los que se compone la información
/// adicional de un comprobante. Sale del modelo fiscal ya construido por el provider (no de una
/// segunda consulta al origen). <see cref="IssueDate"/> es la fecha de negocio del comprobante
/// (ADR-034): toda regla de aplicabilidad se evalúa contra ella, nunca contra el reloj.
/// </summary>
public sealed record AdditionalInfoCompositionContext(
    ElectronicDocumentType DocumentType,
    Guid TenantId,
    Guid CompanyId,
    DateOnly IssueDate,
    ElectronicDocumentIssuerData Issuer
);

/// <summary>
/// ADR-038 D6 — fuente de campos adicionales normativos o sectoriales (p. ej. RUC Proveedor,
/// Anexo 26). Solo el composer los invoca. Un contributor que determina que su campo es obligatorio
/// y no puede producirlo devuelve <c>Failure</c> con un código estable: el XML no se genera.
/// </summary>
public interface IElectronicDocumentAdditionalInfoContributor
{
    /// <summary>Identificador estable para trazabilidad (logs) y desempate de orden.</summary>
    string Id { get; }

    /// <summary>Orden determinístico entre contributors (ascendente; empate → <see cref="Id"/> ordinal).</summary>
    int Order { get; }

    bool AppliesTo(ElectronicDocumentType documentType);

    Task<Result<IReadOnlyList<ElectronicDocumentAdditionalField>>> ContributeAsync(
        AdditionalInfoCompositionContext context,
        CancellationToken ct = default
    );
}

/// <summary>
/// ADR-038 D6 — SSOT de <c>infoAdicional</c>: combina los contributors con los campos propios del
/// documento (<c>sourceFields</c>, p. ej. "Observación") y aplica las reglas del SRI una sola vez.
/// Lo invocan únicamente los orquestadores de ensamblado fiscal
/// (<c>CommercialElectronicDocumentXmlSupplier</c>, <c>RetentionElectronicDocumentXmlService</c>);
/// los builders reciben la lista final.
/// </summary>
public interface IElectronicDocumentAdditionalInfoComposer
{
    Task<Result<IReadOnlyList<ElectronicDocumentAdditionalField>>> ComposeAsync(
        AdditionalInfoCompositionContext context,
        IReadOnlyList<ElectronicDocumentAdditionalField> sourceFields,
        CancellationToken ct = default
    );
}
