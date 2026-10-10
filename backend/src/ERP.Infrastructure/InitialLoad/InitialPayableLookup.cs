using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.InitialLoad;

/// <inheritdoc cref="IInitialPayableLookup"/>
public sealed class InitialPayableLookup : IInitialPayableLookup
{
    private readonly ErpDbContext _db;

    public InitialPayableLookup(ErpDbContext db) => _db = db;

    // AccountsPayable es ICompanyOperationalEntity: el filtro global acota tenant + empresa
    // operativa (fail-closed). Cualquier origen y estado: Compra, Gasto o saldo inicial.
    public async Task<IReadOnlyList<string>> GetDocumentNumbersAsync(Guid supplierId, CancellationToken ct) =>
        await _db.AccountsPayables.AsNoTracking()
            .Where(p => p.SupplierId == supplierId)
            .Select(p => p.DocumentNumber)
            .ToListAsync(ct);
}
