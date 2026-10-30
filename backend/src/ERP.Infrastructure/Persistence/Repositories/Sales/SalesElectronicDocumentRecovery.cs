using ERP.Infrastructure.BackgroundProcessing;
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

    private static IQueryable<SalesReturn> EligibleReturns(IQueryable<SalesReturn> returns) =>
        returns.Where(r => r.Status == SalesReturnStatus.Authorized
            && r.CreditNoteDocumentNumber != null
            && Regex.IsMatch(r.CreditNoteDocumentNumber, DefinitiveNumber)
            && !r.CreditNoteDocumentNumber.EndsWith("-000000000"));

    public async Task<IReadOnlyList<SalesElectronicRecoveryCandidate>> GetCandidatesAsync(int batchSize, CancellationToken ct)
    {
        // Platform discovery only: return identifiers, never tracked business entities.
        // No age cutoff: committed sales remain durable work after any restart.
        var documents = db.ElectronicDocuments.AsPlatformQuery();
        var invoices = Eligible(db.SalesInvoices.AsPlatformQuery().AsNoTracking());
        var missingInvoices = invoices
            .Where(i => !documents.Any(d => d.TenantId == i.TenantId
                && d.SourceModule == "Sales" && d.SourceEntityId == i.Id))
            .Select(i => new { i.Id, i.TenantId, i.CompanyId, i.BranchId, i.CreatedAt,
                DocumentType = ElectronicDocumentType.Invoice });
        // A return inherits its branch/emission point from its own scoped source invoice.
        var missingReturns = from r in EligibleReturns(db.SalesReturns.AsPlatformQuery().AsNoTracking())
            join i in invoices on new { Id = r.SalesInvoiceId, r.TenantId, r.CompanyId }
                equals new { i.Id, i.TenantId, i.CompanyId }
            where !documents.Any(d => d.TenantId == r.TenantId
                && d.SourceModule == "Sales" && d.SourceEntityId == r.Id)
            select new { r.Id, r.TenantId, r.CompanyId, i.BranchId, r.CreatedAt,
                DocumentType = ElectronicDocumentType.CreditNote };
        // One bounded rotating stream for both kinds; neither can starve the other.
        var query = missingInvoices.Concat(missingReturns);
        return await BackgroundJobProgress.ReadBatchAsync(db, "sales-recovery", query,
            i => new SalesElectronicRecoveryCandidate(i.TenantId, i.CompanyId, i.BranchId, i.Id, i.DocumentType),
            i => i.SourceEntityId, i => i.CreatedAt, batchSize, ct);
    }

    public async Task RecoverAsync(SalesElectronicRecoveryCandidate candidate, CancellationToken ct)
    {
        if (candidate.TenantId == Guid.Empty || candidate.CompanyId == Guid.Empty
            || candidate.BranchId == Guid.Empty || candidate.TenantId != tenant.TenantId
            || !company.HasCompanyContext || candidate.CompanyId != company.CompanyId)
            return;

        // Fresh scoped read, with all three ownership dimensions and current state.
        Guid? userId;
        if (candidate.DocumentType == ElectronicDocumentType.Invoice)
            userId = await Eligible(db.SalesInvoices.AsNoTracking())
            .Where(i => i.Id == candidate.SourceEntityId
                && i.TenantId == candidate.TenantId && i.CompanyId == candidate.CompanyId
                && i.BranchId == candidate.BranchId)
            .Select(i => (Guid?)(i.UpdatedBy ?? i.CreatedBy)).SingleOrDefaultAsync(ct);
        else if (candidate.DocumentType == ElectronicDocumentType.CreditNote)
            userId = await (from r in EligibleReturns(db.SalesReturns.AsNoTracking())
                join i in Eligible(db.SalesInvoices.AsNoTracking())
                    on new { Id = r.SalesInvoiceId, r.TenantId, r.CompanyId }
                    equals new { i.Id, i.TenantId, i.CompanyId }
                where r.Id == candidate.SourceEntityId && r.TenantId == candidate.TenantId
                    && r.CompanyId == candidate.CompanyId && i.BranchId == candidate.BranchId
                select (Guid?)(r.UpdatedBy ?? r.CreatedBy)).SingleOrDefaultAsync(ct);
        else
            return;

        if (userId is null || await db.ElectronicDocuments.AnyAsync(d =>
                d.TenantId == candidate.TenantId && d.SourceModule == "Sales"
                && d.SourceEntityId == candidate.SourceEntityId, ct))
            return;

        // Create-only even if another actor inserts after the read. The existing
        // unique source constraint is the final barrier; no commercial effects run.
        var result = await registration.RegisterMissingAsync(new(
            candidate.TenantId, candidate.CompanyId, candidate.DocumentType,
            "Sales", candidate.SourceEntityId, userId.Value), ct);
        if (!result.IsSuccess)
        {
            // A unique-race loser owns a failed tracker. Discard its staged rows
            // before reading the committed winner; never save the losing tracker.
            db.ChangeTracker.Clear();
            if (!await db.ElectronicDocuments.AsNoTracking().AnyAsync(d =>
                    d.TenantId == candidate.TenantId && d.CompanyId == candidate.CompanyId
                    && d.SourceModule == "Sales" && d.SourceEntityId == candidate.SourceEntityId, ct))
                throw new InvalidOperationException(result.Error ?? "Electronic document recovery failed.");
        }
    }
}
