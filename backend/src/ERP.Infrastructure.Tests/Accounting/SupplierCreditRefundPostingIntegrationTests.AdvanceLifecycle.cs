using ERP.Application.Modules.Finance.UseCases;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.Common;
using ERP.Infrastructure.Tests.Audit;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Accounting;

public sealed partial class SupplierCreditRefundPostingIntegrationTests
{
    [Fact]
    public async Task Anticipo_100_aplica_30_reembolsa_40_y_reversa_restaura_70_sin_duplicados()
    {
        var creditId = await SeedAdvanceAsync(100m);
        Guid payableId;
        await using (var seed = CreateContext())
        {
            // Mismo destino ExpenseDocument que SupplierCreditApplyPayablesIntegrationTests.
            var payable = AccountsPayable.CreateFromOrigin(_tenantId, _companyId, _branchId, _supplierId,
                AccountsPayableOriginType.ExpenseDocument, Guid.NewGuid(), "EXP", "EXP-ANTICIPO", _today, _today, _userId);
            payable.AddInstallment(1, _today.AddDays(30), 60m);
            seed.AccountsPayables.Add(payable);
            await seed.SaveChangesAsync();
            payableId = payable.Id;
        }
        await using (var db = BuildWiredContext())
        {
            var company = new FixedCurrentCompany(() => _companyId);
            var handler = new ApplySupplierCreditHandler(new SupplierCreditRepository(db, company),
                new AccountsPayableRepository(db), new PurchaseInvoiceRepository(db, company),
                new PurchaseReturnRepository(db, company), new CompanyRepository(db), new UnitOfWork(db),
                new PostgresDatabaseExceptionTranslator(), new FixedCurrentTenant(() => _tenantId), new FixedCurrentUser(_userId));
            var request = new ApplySupplierCreditCommand(creditId, payableId, 30m, Guid.NewGuid());
            var applied = await handler.Handle(request, CancellationToken.None).ThenDispatchAsync(db);
            applied.IsSuccess.Should().BeTrue(applied.Error);
            (await handler.Handle(request, CancellationToken.None).ThenDispatchAsync(db)).IsSuccess.Should().BeTrue();
        }
        (await AvailableAsync(creditId)).Should().Be(70m);
        var refund = await RefundAsync(creditId, 40m, cash: false);
        refund.IsSuccess.Should().BeTrue(refund.Error);
        (await AvailableAsync(creditId)).Should().Be(30m);
        var movementId = await RefundMovementIdAsync(refund.Value!.Id);
        var reversed = await ReverseAsync(creditId, movementId);
        reversed.IsSuccess.Should().BeTrue(reversed.Error);
        (await AvailableAsync(creditId)).Should().Be(70m);
        await using var verify = CreateContext();
        var credit = await verify.SupplierCredits.AsNoTracking().Include(c => c.Movements).SingleAsync(c => c.Id == creditId);
        credit.OriginalAmount.Should().Be(100m);
        credit.SourceSupplierPaymentId.Should().NotBeNull();
        credit.Movements.Should().HaveCount(3, "aplicación idempotente, reembolso y reversa");
        (await verify.AccountsPayables.AsNoTracking().Include(p => p.Installments).SingleAsync(p => p.Id == payableId)).OutstandingAmount.Should().Be(30m);
        var entries = await verify.JournalEntries.AsNoTracking().Include(e => e.Lines).ToListAsync();
        entries.Should().HaveCount(3, "aplicación, reembolso y espejo histórico");
        entries.Should().OnlyContain(e => e.Lines.Sum(l => l.Debit) == e.Lines.Sum(l => l.Credit));
        var postings = await verify.AccountingPostings.AsNoTracking().ToListAsync();
        postings.Should().HaveCount(3);
        postings.Should().OnlyContain(p => p.Status == ERP.Domain.Modules.Accounting.Enums.AccountingPostingStatus.Posted);
        var mirror = await ReversalLinesAsync(movementId);
        mirror.Should().BeEquivalentTo(new[] { (_advancesLedgerAccountId, 40m, 0m), (_bankLedgerAccountId, 0m, 40m) });
    }
}
