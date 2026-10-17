using ERP.Application.Modules.Accounting.Continuity;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Accounting.Repositories;

/// <summary>
/// ACCOUNTING-CONTINUITY-05 — documentos que generan hechos contables (venta, devolución de venta,
/// compra, gasto, NC y devolución de compra, pago a proveedor, cobro/pago financiero), excluidos
/// borradores y descartes. Las devoluciones sin fecha de negocio propia se evalúan por su instante de
/// registro en UTC (criterio conservador: puede rechazar un caso límite, nunca omitir uno).
/// </summary>
public sealed class AccountingRelevantFactsReader(ErpDbContext db) : IAccountingRelevantFactsReader
{
    public async Task<bool> ExistsOnOrAfterAsync(Guid tenantId, Guid companyId, DateOnly date, CancellationToken ct)
    {
        var instant = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return await db.SalesInvoices.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Status != SalesInvoiceStatus.Draft && x.IssueDate >= date, ct)
            || await db.SalesReturns.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Status != SalesReturnStatus.Draft && x.CreatedAt >= instant, ct)
            || await db.PurchaseInvoices.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && (x.Status == PurchaseStatus.Confirmed || x.Status == PurchaseStatus.Cancelled) && x.IssueDate >= date, ct)
            || await db.ExpenseDocuments.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Status != ExpenseStatus.Draft && x.AccountingDate >= date, ct)
            || await db.PurchaseCreditNotes.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Status != PurchaseCreditNoteStatus.Draft && x.IssueDate >= date, ct)
            || await db.PurchaseReturns.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Status != PurchaseReturnStatus.Draft && x.CreatedAt >= instant, ct)
            || await db.SupplierPayments.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.PaymentDate >= date, ct)
            || await db.Payments.AnyAsync(x => x.TenantId == tenantId && x.CompanyId == companyId
                   && x.Status != PaymentStatus.Draft && x.PaymentDate >= date, ct);
    }
}
