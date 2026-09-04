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
    Task<RetentionDocument?> GetBySourceAsync(
        Guid tenantId,
        Guid companyId,
        RetentionSourceDocumentType sourceType,
        Guid sourceId,
        CancellationToken ct = default
    );
}
