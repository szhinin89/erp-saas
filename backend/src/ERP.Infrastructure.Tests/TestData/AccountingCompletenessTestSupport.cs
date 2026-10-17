using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.InitialLoad;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.InitialLoad;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Services;

namespace ERP.Infrastructure.Tests.TestData;

/// <summary>ACCOUNTING-CONTINUITY-06 — readiness único real sobre un <see cref="ErpDbContext"/> de prueba.</summary>
public static class AccountingCompletenessTestSupport
{
    public static AccountingCompletenessService Create(ErpDbContext db, Guid companyId) =>
        new(
            new AccountingOperationalPolicyRepository(db),
            new CompanyClock(db),
            new AccountingPostingRepository(db),
            new OpeningAccountingCompleteness(
                new OpeningBalanceConstraintsReader(
                    db,
                    Moq.Mock.Of<ICurrentCompany>(c => c.CompanyId == companyId && c.HasCompanyContext)
                ),
                new ImportBatchRepository(db),
                new OpeningBalancePostingRepository(db),
                new OpeningJournalEntryPostingRepository(db)
            ),
            new InventoryCostLedger(db),
            new AccountingRelevantFactsReader(db, new ERP.Infrastructure.Persistence.Services.CompanyClock(db)),
            new JournalEntryRepository(db),
            new AccountingPeriodRepository(db),
            new AccountRepository(db)
        );
}
