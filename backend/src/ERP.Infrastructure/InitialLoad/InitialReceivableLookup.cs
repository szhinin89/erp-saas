using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.InitialLoad;

/// <inheritdoc cref="IInitialReceivableLookup"/>
public sealed class InitialReceivableLookup : IInitialReceivableLookup
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public InitialReceivableLookup(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

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

    // Company es ITenantScopedEntity: el filtro global solo acota por tenant; la empresa es explícita.
    public Task<DateOnly?> GetOpeningBalanceDateAsync(CancellationToken ct) =>
        _db.Companies.AsNoTracking()
            .Where(c => c.Id == _company.CompanyId)
            .Select(c => c.OpeningBalanceDate)
            .FirstOrDefaultAsync(ct);
}
