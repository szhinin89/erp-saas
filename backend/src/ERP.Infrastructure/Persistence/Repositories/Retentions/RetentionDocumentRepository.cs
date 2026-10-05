using ERP.Application.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Retentions;

/// <summary>
/// Fase <c>RETENTIONS-PERSISTENCE-01B</c>. Implementación EF Core de
/// <see cref="IRetentionDocumentRepository"/> — mismo patrón que
/// <c>ExpenseDocumentRepository</c>: fail-closed multi-tenant/company vía
/// <c>ForOperationalScope</c> (tenant siempre filtrado; company solo si hay contexto de company
/// activo); la única excepción es <see cref="GetPendingElectronicStartAsync"/> (job de
/// recuperación, vía <c>AsPlatformQuery</c>).
/// </summary>
public sealed class RetentionDocumentRepository : IRetentionDocumentRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public RetentionDocumentRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    private IQueryable<RetentionDocument> Scoped(Guid tenantId) =>
        _db.Set<RetentionDocument>().ForOperationalScope(tenantId, _company);

    public Task AddAsync(RetentionDocument document, CancellationToken ct = default) =>
        _db.Set<RetentionDocument>().AddAsync(document, ct).AsTask();

    public Task<RetentionDocument?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) => Scoped(tenantId).Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> ExistsActiveBySourceAsync(
        Guid tenantId,
        Guid companyId,
        RetentionSourceDocumentType sourceType,
        Guid sourceId,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Where(x => x.CompanyId == companyId)
            .Where(x => x.SourceDocumentType == sourceType && x.SourceDocumentId == sourceId)
            .Where(x => x.Status != RetentionStatus.Cancelled)
            .AnyAsync(ct);

    public async Task<RetentionStatus?> GetCurrentStatusAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        bool forUpdate,
        CancellationToken ct = default
    )
    {
        // Escalar leído de la BD (no de la instancia trackeada, que puede ser previa al lock).
        // ToListAsync no compone la consulta: el FOR UPDATE queda en la sentencia de nivel superior.
        var statuses = forUpdate
            ? await _db
                .Database.SqlQuery<int>(
                    $"SELECT status AS \"Value\" FROM retention_documents WHERE tenant_id = {tenantId} AND company_id = {companyId} AND id = {id} FOR UPDATE"
                )
                .ToListAsync(ct)
            : await _db
                .Database.SqlQuery<int>(
                    $"SELECT status AS \"Value\" FROM retention_documents WHERE tenant_id = {tenantId} AND company_id = {companyId} AND id = {id}"
                )
                .ToListAsync(ct);
        return statuses.Count == 0 ? null : (RetentionStatus)statuses[0];
    }

    public async Task<
        IReadOnlyList<RetentionElectronicStartCandidate>
    > GetPendingElectronicStartAsync(
        DateTime issuedBeforeUtc,
        int take,
        CancellationToken ct = default
    ) =>
        // Bypass de filtros globales vía el accessor sancionado (AsPlatformQuery), deliberado y
        // acotado: lo usa SOLO el job de recuperación (sin contexto de tenant en Hangfire); devuelve
        // únicamente identificadores (tenant/empresa/retención, correlacionando el comprobante por
        // el MISMO tenant), y cada candidato se procesa después con JobExecutionContext de su propio
        // tenant/empresa, donde los filtros fail-closed vuelven a aplicar.
        await _db.Set<RetentionDocument>()
            .AsPlatformQuery()
            .AsNoTracking()
            .Where(r => r.Status == RetentionStatus.Issued)
            .Where(r => r.UpdatedAt != null && r.UpdatedAt < issuedBeforeUtc)
            .Where(r =>
                !_db
                    .ElectronicDocuments.AsPlatformQuery()
                    .Any(e =>
                        e.TenantId == r.TenantId
                        && e.SourceModule == RetentionElectronicDocumentSource.SourceModule
                        && e.SourceEntityId == r.Id
                        && e.CurrentState != ElectronicDocumentState.Draft
                    )
            )
            .OrderBy(r => r.UpdatedAt)
            .Take(take)
            .Select(r => new RetentionElectronicStartCandidate(r.TenantId, r.CompanyId, r.Id))
            .ToListAsync(ct);

    public Task<RetentionDocument?> GetBySourceAsync(
        Guid tenantId,
        Guid companyId,
        RetentionSourceDocumentType sourceType,
        Guid sourceId,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Include(x => x.Lines)
            .Where(x => x.CompanyId == companyId)
            .Where(x => x.SourceDocumentType == sourceType && x.SourceDocumentId == sourceId)
            .Where(x => x.Status != RetentionStatus.Cancelled)
            .FirstOrDefaultAsync(ct);
}
