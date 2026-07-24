using ERP.Application.Audit;
using ERP.Domain.Audit;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Audit;

/// <summary>
/// Única implementación de <see cref="IAuditWriter{TAudit}"/>, reutilizada por todos los
/// dominios vía registro open-generic en DI — ver <c>DependencyInjection.cs</c>.
/// </summary>
public sealed class EfAuditWriter<TAudit> : IAuditWriter<TAudit> where TAudit : AuditRecordBase
{
    private readonly ErpDbContext _db;

    public EfAuditWriter(ErpDbContext db) => _db = db;

    public async Task RecordAsync(TAudit record, CancellationToken ct = default)
        => await _db.Set<TAudit>().AddAsync(record, ct);
}
