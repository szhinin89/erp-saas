using ERP.Application.Modules.Companies;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.CompanyConfig;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01. Toda consulta filtra explícitamente por (TenantId,
/// CompanyId) — nunca depende de un query filter global, fail-closed multi-tenant.
/// </summary>
public sealed class CompanyPrecisionPolicyRepository : ICompanyPrecisionPolicyRepository
{
    private readonly ErpDbContext _db;

    public CompanyPrecisionPolicyRepository(ErpDbContext db) => _db = db;

    public Task<CompanyPrecisionPolicy?> FindAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    ) =>
        _db.CompanyPrecisionPolicies.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.CompanyId == companyId,
            ct
        );

    public async Task AddAsync(CompanyPrecisionPolicy policy, CancellationToken ct = default) =>
        await _db.CompanyPrecisionPolicies.AddAsync(policy, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task<bool> HasRealOperationsAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    )
    {
        var hasAuthorizedSale = await _db.SalesInvoices.AnyAsync(
            x => x.TenantId == tenantId && x.CompanyId == companyId && x.AuthorizedSubtotal != null,
            ct
        );
        if (hasAuthorizedSale)
            return true;

        var hasConfirmedPurchase = await _db.PurchaseInvoices.AnyAsync(
            x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.Status == PurchaseStatus.Confirmed,
            ct
        );
        if (hasConfirmedPurchase)
            return true;

        var hasConfirmedExpense = await _db.ExpenseDocuments.AnyAsync(
            x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.Status == ExpenseStatus.Confirmed,
            ct
        );
        if (hasConfirmedExpense)
            return true;

        var hasStockMovement = await _db.StockMovements.AnyAsync(
            x => x.TenantId == tenantId && x.CompanyId == companyId,
            ct
        );
        if (hasStockMovement)
            return true;

        var hasCustomerPayment = await _db.Payments.AnyAsync(
            x => x.TenantId == tenantId && x.CompanyId == companyId,
            ct
        );
        if (hasCustomerPayment)
            return true;

        var hasSupplierPayment = await _db.SupplierPayments.AnyAsync(
            x => x.TenantId == tenantId && x.CompanyId == companyId,
            ct
        );
        if (hasSupplierPayment)
            return true;

        var hasPostedJournalEntry = await _db.JournalEntries.AnyAsync(
            x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.Status == JournalEntryStatus.Posted,
            ct
        );
        return hasPostedJournalEntry;
    }
}
