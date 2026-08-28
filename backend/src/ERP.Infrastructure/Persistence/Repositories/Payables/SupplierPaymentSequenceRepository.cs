using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Payables;

/// <summary>
/// Implementación de <see cref="ISupplierPaymentSequenceRepository"/> — mismo criterio que
/// <c>PurchaseReturnSequenceRepository</c> (SUPPLIER-PAYMENTS-AUDIT-15A §3): nunca abre ni comitea
/// una transacción propia, y nunca ejecuta su propio <c>SaveChangesAsync</c> — el incremento de
/// <see cref="SupplierPaymentSequence.CurrentSeq"/> queda trackeado en el <see cref="ErpDbContext"/>
/// ambiente para viajar en el mismo <c>SaveChanges</c> que el resto de efectos de la confirmación
/// del pago.
/// </summary>
public sealed class SupplierPaymentSequenceRepository : ISupplierPaymentSequenceRepository
{
    private const string LockNamespace = "SupplierPayment.Sequence";

    private readonly ErpDbContext _db;

    public SupplierPaymentSequenceRepository(ErpDbContext db) => _db = db;

    public async Task<string> CaptureNextAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    )
    {
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

        var sequence = await _db.SupplierPaymentSequences.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.CompanyId == companyId,
            ct
        );

        if (sequence is null)
        {
            sequence = SupplierPaymentSequence.Create(tenantId, companyId);
            await _db.SupplierPaymentSequences.AddAsync(sequence, ct);
        }

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
