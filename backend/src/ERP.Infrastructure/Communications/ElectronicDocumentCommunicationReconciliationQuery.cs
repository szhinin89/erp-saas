using ERP.Application.Modules.Communications.ElectronicDocuments;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Communications;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 — consulta cross-tenant de la reconciliación (mismo patrón autorizado que
/// <c>RetentionElectronicRecoveryJob</c>/<c>ElectronicDocumentRetryJob</c>): SQL explícito que correlaciona
/// por tenant/empresa y devuelve SOLO identificadores; cada candidato se procesa después en su propio
/// scope bajo <c>JobExecutionContext</c> de su tenant/empresa.
/// <para>
/// "Sin comunicación" = no existe fila en <c>communication_outbox</c> con el mismo tenant, empresa,
/// <c>source_type</c>, <c>source_id</c> y <c>purpose</c> de la ruta (y <c>source_module</c>), en cualquier
/// estado.
/// </para>
/// <para>
/// Sin horizonte de antigüedad (verificación final, medido con EXPLAIN ANALYZE): el límite de 7 días no
/// ahorraba ningún escaneo (<c>created_at</c> no tiene índice en <c>electronic_documents</c>, tabla CLOSED),
/// solo descartaba faltantes. El conjunto de faltantes se calcula SIN <c>LIMIT</c> en un CTE
/// <c>MATERIALIZED</c>: así el planner elige el anti-join por costo (hash/merge) en vez de apostar por un
/// nested loop de "primeras filas" que, con casi todo ya comunicado, sondea cada comprobante. Después se
/// ordena (<c>created_at</c>, <c>id</c>) — determinístico, más antiguos primero — y se pagina por keyset
/// sobre ese conjunto chico.
/// </para>
/// <para>
/// Costo: lineal en comprobantes autorizados + filas de outbox (un anti-join por página), igual que con el
/// límite anterior.
/// </para>
/// </summary>
public sealed class ElectronicDocumentCommunicationReconciliationQuery : IElectronicDocumentCommunicationReconciliationQuery
{
    private readonly ErpDbContext _db;

    public ElectronicDocumentCommunicationReconciliationQuery(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<ElectronicDocumentCommunicationCandidate>> GetMissingAsync(
        IReadOnlyCollection<ElectronicDocumentCommunicationRoute> routes,
        DateTime lastChangeBeforeUtc,
        ElectronicDocumentCommunicationCandidate? after,
        IReadOnlyCollection<Guid> excludedCompanyIds,
        int limit,
        CancellationToken ct = default
    )
    {
        if (routes.Count == 0 || limit <= 0)
            return [];

        var modules = routes.Select(r => r.SourceModule).ToArray();
        var types = routes.Select(r => (int)r.DocumentType).ToArray();
        var sourceTypes = routes.Select(r => r.SourceType).ToArray();
        var purposes = routes.Select(r => r.Purpose).ToArray();
        var excluded = excludedCompanyIds.ToArray();
        var authorized = (int)ElectronicDocumentState.Authorized;
        var afterCreatedAt = after?.CreatedAtUtc ?? DateTime.UnixEpoch;
        var afterId = after?.ElectronicDocumentId ?? Guid.Empty;

        var rows = await _db.Database
            .SqlQuery<CandidateRow>(
                $"""
                WITH missing AS MATERIALIZED (
                    SELECT ed.tenant_id, ed.company_id, ed.id, ed.created_at
                    FROM electronic_documents ed
                    JOIN unnest({modules}::text[], {types}::int[], {sourceTypes}::text[], {purposes}::text[])
                         AS r(source_module, document_type, source_type, purpose)
                      ON r.source_module = ed.source_module AND r.document_type = ed.document_type
                    WHERE ed.current_state = {authorized}
                      AND COALESCE(ed.updated_at, ed.created_at) <= {lastChangeBeforeUtc}
                      AND NOT (ed.company_id = ANY({excluded}::uuid[]))
                      AND NOT EXISTS (
                          SELECT 1 FROM communication_outbox co
                          WHERE co.tenant_id = ed.tenant_id
                            AND co.company_id = ed.company_id
                            AND co.source_type = r.source_type
                            AND co.source_id = ed.source_entity_id
                            AND co.purpose = r.purpose
                            AND co.source_module = ed.source_module)
                )
                SELECT tenant_id AS "TenantId", company_id AS "CompanyId", id AS "ElectronicDocumentId", created_at AS "CreatedAtUtc"
                FROM missing
                WHERE (created_at, id) > ({afterCreatedAt}, {afterId})
                ORDER BY created_at, id
                LIMIT {limit}
                """
            )
            .ToListAsync(ct);

        // timestamptz: Npgsql ya devuelve created_at con Kind = Utc.
        return rows
            .Select(r => new ElectronicDocumentCommunicationCandidate(r.TenantId, r.CompanyId, r.ElectronicDocumentId, r.CreatedAtUtc))
            .ToList();
    }

    private sealed class CandidateRow
    {
        public Guid TenantId { get; init; }
        public Guid CompanyId { get; init; }
        public Guid ElectronicDocumentId { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}
