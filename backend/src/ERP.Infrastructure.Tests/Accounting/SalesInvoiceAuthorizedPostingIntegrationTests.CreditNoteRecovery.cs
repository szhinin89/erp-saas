using ERP.Application;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.Services;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ERP.Infrastructure.Tests.Accounting;

public sealed partial class SalesInvoiceAuthorizedPostingIntegrationTests
{
    [Fact]
    public async Task Authorized_return_without_electronic_document_is_recovered_once_by_registered_sales_recovery()
    {
        // Reuse the existing disposable PostgreSQL harness. Simulate the committed state
        // immediately before post-commit registration; never rerun authorization.
        var invoiceId = await SeedA9Async();
        Guid returnId;
        await using (var seed = A9Context())
        {
            var invoice = await seed.SalesInvoices.Include(i => i.Lines).SingleAsync(i => i.Id == invoiceId);
            var salesReturn = SalesReturn.CreateDraft(_tenantId, _companyId, invoiceId,
                _customerId, "DEV-RECOVERY-001", "Recovery regression", _createdBy);
            salesReturn.AddLine(SalesReturnDetail.Create(salesReturn.Id, _tenantId,
                invoice.Lines.Single().Id, "Servicio A9", 1m, 10m, 0m, "0", 0m, "UNIT"), _createdBy);
            salesReturn.AddRefundAllocation(SalesReturnRefundAllocation.Create(salesReturn.Id,
                _tenantId, SalesReturnRefundMethod.Cash, 10m), _createdBy);
            salesReturn.Authorize(_createdBy);
            salesReturn.SetCreditNoteDocumentNumber("001-001-000000001");
            returnId = salesReturn.Id;
            seed.SalesReturns.Add(salesReturn);
            // The source invoice already has its own document: only the credit note is missing.
            seed.ElectronicDocuments.Add(ElectronicDocument.Create(_tenantId, _companyId,
                ElectronicDocumentType.Invoice, "Sales", invoiceId, _createdBy));
            await seed.SaveChangesAsync();
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
            ["InstallData:Enabled"] = "false",
            ["Snowflake:WorkerId"] = "17"
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(
            new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        services.AddInfrastructure(config);
        services.AddApplication();
        // Only persistence/context plumbing uses the established harness. Recovery and
        // registration retain their production DI registrations and real implementations.
        services.RemoveAll<ErpDbContext>();
        services.AddScoped(_ => A9Context());
        services.RemoveAll<ICurrentTenant>();
        services.AddScoped<ICurrentTenant>(_ => new FixedCurrentTenant(_tenantId));
        services.RemoveAll<ICurrentCompany>();
        services.AddScoped<ICurrentCompany>(_ => new FixedCurrentCompany(_companyId));
        await using var provider = services.BuildServiceProvider();

        await using var before = A9Context();
        var original = await before.SalesReturns.AsNoTracking().Include(r => r.Lines)
            .Include(r => r.RefundAllocations).SingleAsync(r => r.Id == returnId);
        original.Status.Should().Be(SalesReturnStatus.Authorized);
        original.CreditNoteDocumentNumber.Should().Be("001-001-000000001");
        (await before.ElectronicDocuments.AnyAsync(d => d.SourceEntityId == returnId)).Should().BeFalse();
        var sequences = await before.DocumentSequences.AsNoTracking().ToListAsync();
        var stock = await before.StockMovements.AsNoTracking().ToListAsync();
        var cash = await before.CashMovements.AsNoTracking().ToListAsync();
        var receivables = await before.SalesReceivables.AsNoTracking().ToListAsync();
        var journals = await before.JournalEntries.AsNoTracking().ToListAsync();

        // Exactly the discovery -> scoped recovery path used by the registered sales job.
        // Do not fabricate a candidate or call RegisterMissingAsync directly for the return.
        for (var run = 0; run < 2; run++)
        {
            await using var discovery = provider.CreateAsyncScope();
            var recovery = discovery.ServiceProvider.GetRequiredService<ISalesElectronicDocumentRecovery>();
            recovery.Should().BeOfType<SalesElectronicDocumentRecovery>();
            var candidates = await recovery.GetCandidatesAsync(50, default);
            foreach (var candidate in candidates)
            {
                using var context = ERP.Infrastructure.Services.JobExecutionContext.Begin(candidate.TenantId, candidate.CompanyId);
                await using var scope = provider.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISalesElectronicDocumentRecovery>()
                    .RecoverAsync(candidate, default);
            }
            await using var round = A9Context();
            var recovered = await round.ElectronicDocuments.AsNoTracking()
                .Where(d => d.SourceModule == "Sales" && d.SourceEntityId == returnId).ToListAsync();
            recovered.Should().ContainSingle($"recovery run {run + 1} must leave exactly one credit note")
                .Which.DocumentType.Should().Be(ElectronicDocumentType.CreditNote);
            recovered.Single().CurrentState.Should().Be(ElectronicDocumentState.Draft);
        }

        await using var verify = A9Context();
        using var assertions = new AssertionScope();
        (await verify.SalesReturns.AsNoTracking().Include(r => r.Lines).Include(r => r.RefundAllocations)
            .SingleAsync(r => r.Id == returnId)).Should().BeEquivalentTo(original);
        (await verify.DocumentSequences.AsNoTracking().ToListAsync()).Should().BeEquivalentTo(sequences);
        (await verify.StockMovements.AsNoTracking().ToListAsync()).Should().BeEquivalentTo(stock);
        (await verify.CashMovements.AsNoTracking().ToListAsync()).Should().BeEquivalentTo(cash);
        (await verify.SalesReceivables.AsNoTracking().ToListAsync()).Should().BeEquivalentTo(receivables);
        (await verify.JournalEntries.AsNoTracking().ToListAsync()).Should().BeEquivalentTo(journals);
        var documents = await verify.ElectronicDocuments.AsNoTracking()
            .Where(d => d.SourceModule == "Sales" && d.SourceEntityId == returnId).ToListAsync();
        documents.Should().ContainSingle("the registered recovery must recover the missing credit note")
            .Which.Should().Match<ElectronicDocument>(d => d.DocumentType == ElectronicDocumentType.CreditNote
                && d.TenantId == _tenantId && d.CompanyId == _companyId);
    }
}
