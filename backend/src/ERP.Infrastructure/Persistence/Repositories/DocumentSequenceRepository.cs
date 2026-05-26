using ERP.Application.Common;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class DocumentSequenceRepository : IDocumentSequenceRepository
{
    private readonly ErpDbContext          _db;
    private readonly IPlatformQueryAccessor _platform;

    public DocumentSequenceRepository(ErpDbContext db, IPlatformQueryAccessor platform)
    {
        _db       = db;
        _platform = platform;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Usa <c>SELECT … FOR UPDATE</c> para bloqueo pesimista por fila.
    /// El caller es responsable de abrir la transacción antes de llamar este método.
    /// Lock granular: (emission_point_id, doc_type_code) — no bloquea toda la empresa.
    /// </remarks>
    public Task<DocumentSequence?> GetForUpdateAsync(Guid emissionPointId, string docTypeCode, CancellationToken ct = default)
        => _db.DocumentSequences
            .FromSqlRaw(
                "SELECT * FROM document_sequence WHERE emission_point_id = {0} AND doc_type_code = {1} FOR UPDATE",
                emissionPointId, docTypeCode)
            .FirstOrDefaultAsync(ct);

    public Task<bool> ExistsAsync(Guid emissionPointId, string docTypeCode, CancellationToken ct = default)
        => _platform
            .Unfiltered(_db.DocumentSequences, PlatformQueryReason.Seeding)
            .AnyAsync(ds => ds.EmissionPointId == emissionPointId
                         && ds.DocTypeCode     == docTypeCode, ct);

    public Task AddAsync(DocumentSequence sequence, CancellationToken ct = default)
    {
        _db.DocumentSequences.Add(sequence);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
