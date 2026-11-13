using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.BackgroundProcessing;

/// <summary>
/// ADR-048 — pendiente pausado por el estado de la empresa, para observabilidad (log estructurado y métrica):
/// identificador técnico de empresa, cantidad, antigüedad del más antiguo y motivo. Sin comprobantes,
/// claves de acceso, tokens, destinatarios ni datos personales.
/// </summary>
public sealed record CompanyPausedPending(
    Guid CompanyId,
    long Count,
    DateTime? OldestUtc,
    CompanyPauseReason Reason
)
{
    public double OldestAgeHours =>
        OldestUtc is { } oldest ? Math.Max(0, (DateTime.UtcNow - oldest).TotalHours) : 0;
}

public static class CompanyPausedPendingQueries
{
    /// <summary>
    /// Comprobantes Draft/Failed (transmisión nueva pendiente: nunca salieron al SRI) de empresas suspendidas o
    /// deshabilitadas, por empresa. Los comprobantes de envío previo o incierto no se pausan y no se cuentan.
    /// </summary>
    public static async Task<IReadOnlyList<CompanyPausedPending>> ElectronicDocumentsAsync(
        ErpDbContext db,
        int top,
        CancellationToken ct
    )
    {
        var rows = await (
            from d in db.ElectronicDocuments.AsPlatformQuery()
            where d.CurrentState == ElectronicDocumentState.Draft || d.CurrentState == ElectronicDocumentState.Failed
            join c in db.Companies.AsPlatformQuery() on new { Id = d.CompanyId, d.TenantId } equals new { c.Id, c.TenantId }
            where !c.IsActive || c.OperationalStatus == ERP.Domain.Modules.Company.Enums.CompanyOperationalStatus.Suspended
            group d by new { d.CompanyId, c.IsActive, c.OperationalStatus } into g
            orderby g.Min(x => x.CreatedAt)
            select new
            {
                g.Key.CompanyId,
                g.Key.IsActive,
                g.Key.OperationalStatus,
                Count = g.LongCount(),
                Oldest = (DateTime?)g.Min(x => x.CreatedAt),
            }
        )
            .Take(top)
            .ToListAsync(ct);
        return rows
            .Select(r => new CompanyPausedPending(
                r.CompanyId,
                r.Count,
                r.Oldest,
                CompanyAutomaticProcessingPause.ReasonOf(r.IsActive, r.OperationalStatus)
            ))
            .ToList();
    }

    /// <summary>Gauge agregado (sin etiqueta de empresa: baja cardinalidad); el detalle por empresa va al log.</summary>
    public static void RecordMetric(string stream, IReadOnlyList<CompanyPausedPending> rows) =>
        JobMetrics.RecordBacklog(
            rows.Sum(r => r.Count),
            rows.Count == 0 ? null : rows.Min(r => r.OldestUtc),
            stream
        );
}
