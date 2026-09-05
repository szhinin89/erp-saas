using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Infrastructure.Persistence;
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
        // AuthorizeSalesInvoiceHandler, IssueRetentionUseCases), el comportamiento es exactamente
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

            // ZH-AUTH-DOCUMENT-SEQUENCE-COMPANY-SQL-SCOPE-09 — clave lógica completa
            // (TenantId + CompanyId + EmissionPointId + DocTypeCode) en el propio predicado, no
            // solo (EmissionPointId, DocTypeCode): defensa adicional a AsPlatformQuery() para que
            // un tenantId/companyId inconsistente con el dueño real de la secuencia nunca
            // encuentre por accidente una fila de otro scope.
            var existing = await _db
                .DocumentSequences.AsPlatformQuery()
                .FirstOrDefaultAsync(
                    s =>
                        s.TenantId == tenantId
                        && s.CompanyId == companyId
                        && s.EmissionPointId == emissionPointId
                        && s.DocTypeCode == docTypeCode,
                    ct
                );

            int seqValue;
            if (existing is null)
            {
                // Antes de crear una secuencia nueva, verificar que no exista ya una bajo OTRO
                // tenant/company para este mismo EmissionPointId+DocTypeCode: EmissionPointId es
                // único globalmente (un punto de emisión pertenece a una sola empresa para
                // siempre — ver EmissionPoint), así que cualquier fila preexistente con este
                // mismo EmissionPointId+DocTypeCode pertenece necesariamente al dueño real. Sin
                // este chequeo, un tenantId/companyId inconsistente pasado por el caller crearía
                // una segunda fila fantasma (uq_doc_seq no lo impide, porque el scope declarado
                // es distinto) y esa numeración SRI quedaría duplicada/dividida en silencio. No
                // requiere JOIN a emission_points — sigue siendo una consulta sobre la propia
                // tabla document_sequence.
                var ownedByAnotherScope = await _db
                    .DocumentSequences.AsPlatformQuery()
                    .AnyAsync(
                        s =>
                            s.EmissionPointId == emissionPointId
                            && s.DocTypeCode == docTypeCode
                            && (s.TenantId != tenantId || s.CompanyId != companyId),
                        ct
                    );
                if (ownedByAnotherScope)
                    throw new InvalidOperationException(
                        $"No se puede capturar el secuencial: ya existe una secuencia para el punto "
                            + $"de emisión '{emissionPointId}' y tipo documental '{docTypeCode}' bajo "
                            + "otra empresa/tenant. El tenantId/companyId recibido es inconsistente con "
                            + "el dueño real de esa secuencia."
                    );

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
                // La condición completa de scope en el WHERE (además de id, que ya es único) es
                // defensa en profundidad explícita, redundante con el SELECT que cargó `existing`
                // bajo el mismo scope — no cambia el comportamiento, documenta la garantía.
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE document_sequence
                    SET current_seq = {nextSeq}, has_been_used = TRUE, updated_at = {now}
                    WHERE id = {existing.Id}
                        AND tenant_id = {tenantId}
                        AND company_id = {companyId}
                        AND emission_point_id = {emissionPointId}
                        AND doc_type_code = {docTypeCode}
                    """,
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DocumentSequence>> GetAllAsync(
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .DocumentSequences.OrderBy(s => s.EmissionPointId)
            .ThenBy(s => s.DocTypeCode)
            .ToListAsync(cancellationToken);

    public async Task<DocumentSequence?> GetForUpdateAsync(
        Guid tenantId,
        Guid companyId,
        Guid emissionPointId,
        string docTypeCode,
        CancellationToken cancellationToken = default
    )
    {
        return await _db
            .DocumentSequences.FromSqlInterpolated(
                $"""
                SELECT * FROM document_sequence
                WHERE tenant_id = {tenantId}
                    AND company_id = {companyId}
                    AND emission_point_id = {emissionPointId}
                    AND doc_type_code = {docTypeCode}
                FOR UPDATE
                """
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
