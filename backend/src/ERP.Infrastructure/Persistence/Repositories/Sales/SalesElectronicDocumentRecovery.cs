using System.Text.RegularExpressions;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Sales;

public sealed class SalesElectronicDocumentRecovery(
    ErpDbContext db, ICurrentTenant tenant, ICurrentCompany company,
    IElectronicDocumentRegistration registration) : ISalesElectronicDocumentRecovery
{
    private const string DefinitiveNumber = "^[0-9]{3}-[0-9]{3}-[0-9]{9}$";

    private static IQueryable<SalesInvoice> Eligible(IQueryable<SalesInvoice> invoices) =>
        invoices.Where(i => i.Status == SalesInvoiceStatus.Authorized
            && i.EmissionType == EmissionType.Electronic
            && i.EmissionPointId != null
            && Regex.IsMatch(i.InvoiceNumber, DefinitiveNumber)
            && !i.InvoiceNumber.EndsWith("-000000000"));

    public async Task<IReadOnlyList<SalesElectronicRecoveryCandidate>> GetCandidatesAsync(CancellationToken ct)
    {
        // Platform discovery only: return identifiers, never tracked business entities.
        // No age cutoff: committed sales remain durable work after any restart.
        var documents = db.ElectronicDocuments.AsPlatformQuery();
        return await Eligible(db.SalesInvoices.AsPlatformQuery().AsNoTracking())
            .Where(i => !documents.Any(d => d.TenantId == i.TenantId
                && d.SourceModule == "Sales" && d.SourceEntityId == i.Id))
            .OrderBy(i => i.CreatedAt).ThenBy(i => i.Id)
            .Select(i => new SalesElectronicRecoveryCandidate(i.TenantId, i.CompanyId, i.BranchId, i.Id))
            .ToListAsync(ct);
    }

    public async Task RecoverAsync(SalesElectronicRecoveryCandidate candidate, CancellationToken ct)
    {
        if (candidate.TenantId == Guid.Empty || candidate.CompanyId == Guid.Empty
            || candidate.BranchId == Guid.Empty || candidate.TenantId != tenant.TenantId
            || !company.HasCompanyContext || candidate.CompanyId != company.CompanyId)
            return;

        // Fresh scoped read, with all three ownership dimensions and current state.
        var invoice = await Eligible(db.SalesInvoices.AsNoTracking())
            .SingleOrDefaultAsync(i => i.Id == candidate.InvoiceId
                && i.TenantId == candidate.TenantId && i.CompanyId == candidate.CompanyId
                && i.BranchId == candidate.BranchId, ct);
        if (invoice is null || await db.ElectronicDocuments.AnyAsync(d =>
                d.TenantId == candidate.TenantId && d.SourceModule == "Sales"
                && d.SourceEntityId == candidate.InvoiceId, ct))
            return;

        // Create-only even if another actor inserts after the read. The existing
        // unique source constraint is the final barrier; no commercial effects run.
        var result = await registration.RegisterMissingAsync(new(
            invoice.TenantId, invoice.CompanyId, ElectronicDocumentType.Invoice,
            "Sales", invoice.Id, invoice.UpdatedBy ?? invoice.CreatedBy), ct);
        if (!result.IsSuccess)
        {
            // A unique-race loser owns a failed tracker. Discard its staged rows
            // before reading the committed winner; never save the losing tracker.
            db.ChangeTracker.Clear();
            if (!await db.ElectronicDocuments.AsNoTracking().AnyAsync(d =>
                    d.TenantId == candidate.TenantId && d.CompanyId == candidate.CompanyId
                    && d.SourceModule == "Sales" && d.SourceEntityId == candidate.InvoiceId, ct))
                throw new InvalidOperationException(result.Error ?? "Electronic document recovery failed.");
        }
    }
}
