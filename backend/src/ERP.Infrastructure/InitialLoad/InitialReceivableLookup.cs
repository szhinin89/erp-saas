using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.InitialLoad;

/// <inheritdoc cref="IInitialReceivableLookup"/>
public sealed class InitialReceivableLookup : IInitialReceivableLookup
{
    private readonly ErpDbContext _db;

    public InitialReceivableLookup(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> GetDocumentNumbersAsync(Guid customerId, CancellationToken ct)
    {
        var initialBalances = await _db.SalesReceivables.AsNoTracking()
            .Where(r => r.CustomerId == customerId && r.DocumentNumberNormalized != null)
            .Select(r => r.DocumentNumberNormalized!)
            .ToListAsync(ct);
        var invoices = await _db.SalesReceivables.AsNoTracking()
            .Where(r => r.CustomerId == customerId && r.InvoiceId != null)
            .Join(_db.SalesInvoices.AsNoTracking(), r => r.InvoiceId, i => (Guid?)i.Id, (_, i) => i.InvoiceNumber)
            .ToListAsync(ct);
        return [.. initialBalances, .. invoices.Where(n => !string.IsNullOrWhiteSpace(n))];
    }
}
