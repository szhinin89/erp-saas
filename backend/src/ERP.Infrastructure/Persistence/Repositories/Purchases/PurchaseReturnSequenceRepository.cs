using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Purchases;

/// <summary>
/// Implementación de <see cref="IPurchaseReturnSequenceRepository"/> — diseño P0-02 §7.1bis, Fase 2.
/// A diferencia de <c>DocumentSequenceRepository</c>, <see cref="CaptureNextAsync"/> nunca abre ni
/// comitea una transacción propia bajo ninguna circunstancia (corrección de diseño explícita —
/// §7.1bis): se invoca exclusivamente desde dentro de la transacción ambiente ya abierta por
/// <c>AuthorizePurchaseReturnUseCases</c> (Fase 6). Tampoco ejecuta su propio
/// <c>SaveChangesAsync</c> — el incremento de <see cref="PurchaseReturnSequence.CurrentSeq"/> queda
/// únicamente trackeado en el <see cref="ErpDbContext"/> ambiente, para viajar en el mismo
/// <c>SaveChangesWithSequenceRetryAsync</c> que el resto de efectos de <c>Authorize()</c> (paso 9 de
/// §7.1bis) — nunca en un <c>SaveChanges</c> separado.
/// </summary>
public sealed class PurchaseReturnSequenceRepository : IPurchaseReturnSequenceRepository
{
    // Namespace de hash independiente de "PurchaseInvoice.FinancialLock"/"SupplierCredit.Lock"/
    // "SalesReturn.Lock"/IJournalEntryRepository.AcquireIdempotencyLockAsync (§7.1bis paso 3).
    private const string LockNamespace = "PurchaseReturn.Sequence";

    private readonly ErpDbContext _db;

    public PurchaseReturnSequenceRepository(ErpDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<string> CaptureNextAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    )
    {
        // Paso 3 (§7.1bis): pg_advisory_xact_lock con ámbito de transacción — se libera
        // automáticamente al COMMIT/ROLLBACK de la transacción ambiente ya abierta por el caller.
        var hash1 = StableHash(
            System
                .Text.Encoding.UTF8.GetBytes(LockNamespace)
                .Concat(tenantId.ToByteArray())
                .ToArray()
        );
        var hash2 = StableHash(companyId.ToByteArray());
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({hash1}, {hash2})",
            ct
        );

        // Paso 2/4 (§7.1bis): fila creada on-demand si no existe — dentro de la misma transacción
        // ambiente, sin abrir una transacción propia para la creación perezosa.
        var sequence = await _db.PurchaseReturnSequences.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.CompanyId == companyId,
            ct
        );

        if (sequence is null)
        {
            sequence = PurchaseReturnSequence.Create(tenantId, companyId);
            await _db.PurchaseReturnSequences.AddAsync(sequence, ct);
        }

        // Pasos 5-7 (§7.1bis): lectura + incremento en memoria + formateo D8, sobre la misma
        // entidad trackeada por el DbContext ambiente — persistencia diferida al SaveChanges único
        // de Authorize() (paso 9), nunca aquí.
        return sequence.CaptureAndIncrement();
    }

    private static int StableHash(byte[] bytes)
    {
        int h = 17;
        foreach (var b in bytes)
            h = unchecked(h * 31 + b);
        return h;
    }
}
