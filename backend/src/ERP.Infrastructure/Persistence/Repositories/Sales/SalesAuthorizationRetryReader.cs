using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.Modules.Sales.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Sales;

public sealed class SalesAuthorizationRetryReader(
    DbContextOptions<ErpDbContext> options,
    ICurrentTenant tenant,
    ICurrentCompany company,
    IPublisher publisher,
    IDatabaseExceptionTranslator exceptions) : ISalesAuthorizationRetryReader
{
    public bool IsConflict(Exception exception) =>
        exceptions.ClassifyFailureCode(exception) is
            ApiResponseCodes.Common.ConcurrencyConflict or ApiResponseCodes.Common.UniqueViolation;

    public async Task<CommittedSalesAuthorization?> ReadAuthorizedAsync(
        Guid tenantId, Guid companyId, Guid branchId, Guid invoiceId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty || branchId == Guid.Empty
            || tenantId != tenant.TenantId || !company.HasCompanyContext || companyId != company.CompanyId)
            return null;

        // No failed tracker, transaction or connection is reused; this context only reads.
        await using var db = new ErpDbContext(options, tenant, publisher, company);
        var invoice = await db.SalesInvoices.AsNoTracking()
            .Include(i => i.Lines.OrderBy(l => l.SortOrder)).ThenInclude(l => l.Taxes)
            .Include(i => i.Payments)
            .Include(i => i.PaymentSchedules.OrderBy(s => s.InstallmentNumber))
            .SingleOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == tenantId
                && i.CompanyId == companyId && i.BranchId == branchId
                && i.Status == SalesInvoiceStatus.Authorized, ct);
        if (invoice is null)
            return null;

        var document = await db.ElectronicDocuments.AsNoTracking().SingleOrDefaultAsync(
            d => d.TenantId == tenantId && d.CompanyId == companyId
                && d.SourceModule == "Sales" && d.SourceEntityId == invoiceId, ct);
        return new(invoice, document);
    }
}
