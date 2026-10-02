using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;

namespace ERP.Domain.Modules.Retentions.Interfaces;

/// <summary>
/// Contrato de persistencia de <see cref="RetentionDocument"/> — vive en Domain, mismo criterio ya
/// usado por <c>IExpenseDocumentRepository</c>/<c>IPurchaseInvoiceRepository</c>. Sin
/// implementación en esta fase (<c>RETENTIONS-FOUNDATION-01A</c>) — Infrastructure/EF quedan para
/// <c>E1-B</c>.
/// </summary>
public interface IRetentionDocumentRepository
{
    Task AddAsync(RetentionDocument document, CancellationToken ct = default);

    Task<RetentionDocument?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Indica si existe una retención "activa" (<c>Status != Cancelled</c>, es decir <c>Draft</c> o
    /// <c>Issued</c>) sobre el documento origen dado. Decisión de diseño: <c>Draft</c> cuenta como
    /// activa a propósito — mientras exista un borrador sin cancelar sobre el mismo origen, no debe
    /// permitirse crear otro, para preservar la unicidad por origen (ver
    /// <c>docs/decisions/RETENTIONS-MODULE-DESIGN-01.md</c> § "Agregado raíz" / "Impacto en CxP")
    /// que evita que <c>AccountsPayable.ReverseRetention()</c> — el cual revierte el total
    /// retenido del AP, no un monto específico — pueda revertir de más si alguna vez existiera más
    /// de una retención activa sobre el mismo origen.
    /// </summary>
    Task<bool> ExistsActiveBySourceAsync(
        Guid tenantId,
        Guid companyId,
        RetentionSourceDocumentType sourceType,
        Guid sourceId,
        CancellationToken ct = default
    );

    /// <summary>
    /// RETENTIONS-APPLICATION-01C — trae la retención "activa" (<c>Status != Cancelled</c>) sobre
    /// el documento origen dado, con sus líneas incluidas. Mismo criterio de "activa" ya usado por
    /// <see cref="ExistsActiveBySourceAsync"/> — a diferencia de ese método (solo booleano), este
    /// devuelve la entidad completa para <c>GetRetentionBySourceQuery</c>. Fail-closed
    /// tenant/company vía el mismo scope que el resto del repositorio, nunca
    /// <c>IgnoreQueryFilters</c>.
    /// </summary>
    /// <summary>
    /// ADR-036 — estado ACTUAL en BD de la retención (escalar, nunca la instancia trackeada), filtrado
    /// por tenant y empresa; <c>null</c> si no existe en ese alcance. Con <paramref name="forUpdate"/>
    /// toma <c>SELECT … FOR UPDATE</c> sobre la fila (solo dentro de una transacción abierta): es el
    /// lock que serializa el reclamo de envío electrónico contra la anulación del origen.
    /// </summary>
    Task<RetentionStatus?> GetCurrentStatusAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        bool forUpdate,
        CancellationToken ct = default
    );

    /// <summary>
    /// ADR-036 / ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A (recovery) — retenciones <c>Issued</c> de
    /// CUALQUIER tenant/empresa (uso exclusivo del job de recuperación, que fija el contexto por
    /// candidato) emitidas antes de <paramref name="issuedBeforeUtc"/> cuya transmisión no arrancó:
    /// sin ElectronicDocument, o con uno que quedó en Draft (interrumpido antes de correr el pipeline).
    /// </summary>
    Task<IReadOnlyList<RetentionElectronicStartCandidate>> GetPendingElectronicStartAsync(
        DateTime issuedBeforeUtc,
        int take,
        CancellationToken ct = default
    );

    Task<RetentionDocument?> GetBySourceAsync(
        Guid tenantId,
        Guid companyId,
        RetentionSourceDocumentType sourceType,
        Guid sourceId,
        CancellationToken ct = default
    );
}

/// <summary>Retención emitida que todavía no inició su transmisión electrónica.</summary>
public sealed record RetentionElectronicStartCandidate(Guid TenantId, Guid CompanyId, Guid RetentionId);
