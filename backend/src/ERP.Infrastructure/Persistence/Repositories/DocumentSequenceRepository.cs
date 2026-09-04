using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Globalization;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class DocumentSequenceRepository : IDocumentSequenceRepository
{
    private readonly ErpDbContext _db;

    public DocumentSequenceRepository(ErpDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<string> CaptureNextAsync(
        Guid tenantId,
        Guid companyId,
        Guid emissionPointId,
        string docTypeCode,
        CancellationToken ct = default
    )
    {
        // Si el caller ya abrió una transacción ambiente (p. ej. para sostener su propio
        // advisory lock durante toda una unidad de trabajo — mismo patrón que
        // IJournalEntryRepository.AcquireIdempotencyLockAsync/JournalEntrySequenceRepository),
        // esta captura debe correr DENTRO de esa transacción en vez de abrir la suya: Npgsql no
        // soporta transacciones anidadas sobre la misma conexión. pg_advisory_xact_lock se ata a
        // "la transacción actualmente activa en la conexión" sea cual sea su dueño, así que la
        // garantía de exclusión mutua es idéntica en ambos casos — solo cambia quién hace
        // commit/rollback. Cuando no hay transacción ambiente (todos los callers existentes hoy:
        // AuthorizeSalesInvoiceHandler, IssueWithholdingUseCases), el comportamiento es exactamente
        // el de antes: transacción propia, commit inmediato aquí mismo.
        var ownsTransaction = _db.Database.CurrentTransaction is null;
        IDbContextTransaction? tx = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct)
            : null;

        try
        {
            // Advisory lock de transacción — serializa concurrentes para el mismo
            // (emissionPointId, docTypeCode) sin bloquear otros puntos/tipos.
            // pg_advisory_xact_lock(int4, int4) se libera automáticamente al hacer COMMIT/ROLLBACK
            // de la transacción activa (propia o ambiente).
            var epHash = StableHash(emissionPointId.ToByteArray());
            var docHash = StableHash(System.Text.Encoding.UTF8.GetBytes(docTypeCode));
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({epHash}, {docHash})",
                ct
            );

            // Buscar fila existente SIN aplicar QueryFilters globales para evitar
            // interferencia con interceptores de tenant en esta transacción dedicada.
            var existing = await _db
                .DocumentSequences.IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    s => s.EmissionPointId == emissionPointId && s.DocTypeCode == docTypeCode,
                    ct
                );

            int seqValue;
            if (existing is null)
            {
                // Crear secuencia on-demand; primer valor de CurrentSeq es 1 → primer documento = "000000001".
                var newId = Guid.NewGuid();
                var now = DateTime.UtcNow;
                seqValue = 1;
                var nextSeq = 2;
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO document_sequence
                        (id, tenant_id, company_id, emission_point_id, doc_type_code, current_seq, has_been_used, created_at, updated_at)
                    VALUES
                        ({newId}, {tenantId}, {companyId}, {emissionPointId}, {docTypeCode}, {nextSeq}, TRUE, {now}, {now})
                    """,
                    ct
                );
            }
            else
            {
                // GREATEST(CurrentSeq, 1): protección defensiva contra filas antiguas con 0.
                seqValue = Math.Max(existing.CurrentSeq, 1);
                var nextSeq = seqValue + 1;
                var now = DateTime.UtcNow;
                // has_been_used pasa a TRUE aquí igual que en el INSERT — DOCUMENT-SEQUENCES-CONFIG-03:
                // esta es la única vía real de captura (CaptureNextAsync), así que a partir de esta
                // línea la fila queda marcada como "usada" y ConfigureDocumentSequenceCommand ya no
                // puede reconfigurar su número inicial libremente.
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE document_sequence SET current_seq = {nextSeq}, has_been_used = TRUE, updated_at = {now} WHERE id = {existing.Id}",
                    ct
                );
            }

            // Si la transacción es propia, el commit ocurre aquí mismo (comportamiento idéntico
            // al de antes). Si es ambiente, el commit/rollback queda enteramente a cargo del
            // caller — igual que AcquireReturnLockAsync/AcquireIdempotencyLockAsync.
            if (ownsTransaction)
                await tx!.CommitAsync(ct);

            return seqValue.ToString("D9", CultureInfo.InvariantCulture);
        }
        finally
        {
            if (tx is not null)
                await tx.DisposeAsync();
        }
    }

    /// <inheritdoc/>
    public Task<DocumentSequence?> GetByEmissionPointAndDocTypeAsync(
        Guid emissionPointId,
        string docTypeCode,
        CancellationToken cancellationToken = default
    ) =>
        _db.DocumentSequences.FirstOrDefaultAsync(
            s => s.EmissionPointId == emissionPointId && s.DocTypeCode == docTypeCode,
            cancellationToken
        );

    public async Task<DocumentSequence?> GetForUpdateAsync(
        Guid emissionPointId,
        string docTypeCode,
        CancellationToken cancellationToken = default
    )
    {
        return await _db
            .DocumentSequences.FromSqlInterpolated(
                $"SELECT * FROM document_sequence WHERE emission_point_id = {emissionPointId} AND doc_type_code = {docTypeCode} FOR UPDATE"
            )
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(
        Guid emissionPointId,
        string docTypeCode,
        CancellationToken cancellationToken = default
    ) =>
        _db.DocumentSequences.AnyAsync(
            s => s.EmissionPointId == emissionPointId && s.DocTypeCode == docTypeCode,
            cancellationToken
        );

    public Task AddAsync(
        DocumentSequence sequence,
        CancellationToken cancellationToken = default
    ) => _db.DocumentSequences.AddAsync(sequence, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    // Hash estable (no depende de HashCode.GetHashCode que es no-determinístico en .NET 5+).
    private static int StableHash(ReadOnlySpan<byte> bytes)
    {
        int h = 17;
        foreach (var b in bytes)
            h = unchecked(h * 31 + b);
        return h;
    }
}
