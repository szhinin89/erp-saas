using ERP.Application.Common;
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
/// activo), nunca <c>IgnoreQueryFilters</c>.
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

    public Task<RetentionDocument?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        Scoped(tenantId).Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);

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
}
