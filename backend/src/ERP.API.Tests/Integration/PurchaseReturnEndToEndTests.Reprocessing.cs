using ERP.API.Tests.Support;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Tests.Integration;

public sealed partial class PurchaseReturnEndToEndTests
{
    private const string ReprocessingKey = "2110202604179135268800110010010000000991234567810";

    private PurchaseCreditNoteReprocessingGuard ReprocessingGuard(ErpDbContext db,
        SriFiscalStatus status = SriFiscalStatus.Authorized) => new(
        new PurchaseCreditNoteRepository(db, new FixedCurrentCompany(() => _companyId)),
        new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
        new AccountsPayableRepository(db), new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
        new StockRepository(db, new FixedCurrentCompany(() => _companyId), new RealDatabaseExceptionTranslator(), StandardPrecisionPolicyProvider.Instance),
        new JournalEntryRepository(db), new AccountingPostingRepository(db),
        new AccountingOperationalDecision(new AccountingOperationalPolicyRepository(db)),
        new SriSettingsRepository(db), new ReprocessingSriQuery(status), new UnitOfWork(db),
        StandardPrecisionPolicyProvider.Instance);

    private async Task<(CreateDraftPurchaseCreditNoteCommand Command, Guid NoteId)> CancelledNoteForReprocessing(
        bool discount = false, bool cancel = true, decimal unitPrice = 100m, decimal quantity = 10m,
        decimal? paidAmount = null, decimal freightCost = 0m, decimal? legacyExitTotalCost = null)
    {
        await using (var setup = CreateContext())
        {
            var initialPolicy = await setup.AccountingOperationalPolicies.SingleAsync();
            initialPolicy.EndBefore(new DateOnly(2000, 1, 1), _userId);
            setup.AccountingOperationalPolicies.Add(AccountingOperationalPolicy.Create(_tenantId, _companyId,
                AccountingOperationalMode.Disabled, new DateOnly(2000, 1, 1), "Prueba aislada sin contabilización", _userId));
            setup.SriSettings.Add(SriSettings.Create(_tenantId, _companyId, 1, 1, "https://sri.invalid/test", _userId));
            await setup.SaveChangesAsync();
        }
        var invoice = await SeedConfirmedInvoiceAsync(unitPrice, quantity, paidAmount ?? (discount ? 0m : 1000m), freightCost);
        var command = UnifiedReturnCommand(invoice) with { CreditNoteNumber = "001-001-000000099", AccessKey = ReprocessingKey };
        Guid id;
        await using (var db = CreateContext())
        {
            if (discount)
            {
                var source = await new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId))
                    .GetByIdAsync(_tenantId, invoice.InvoiceId);
                command = command with { ApplicationType = PurchaseCreditNoteApplicationType.Discount,
                    ReturnLines = [], TaxSummaryLines = [new(source!.TaxSummaries.Single().Id, 100m)] };
            }
            var created = await UnifiedDraftHandler(db, ReprocessingGuard(db)).Handle(command, default);
            created.IsSuccess.Should().BeTrue(created.Error);
            id = created.Value!.Id;
        }
        await using (var db = CreateContext())
        {
            var authorized = await UnifiedAuthorizeHandler(db).Handle(new(id, Guid.NewGuid()), default);
            authorized.IsSuccess.Should().BeTrue(authorized.Error);
        }
        if (legacyExitTotalCost is { } legacy)
        {
            // Fixture aislada: emula una salida registrada antes del importe monetario canónico
            // (TotalCost con 6 decimales). Producción nunca modifica un movimiento existente.
            await using var legacyDb = CreateContext();
            var returnId = (await legacyDb.PurchaseCreditNotes.SingleAsync(n => n.Id == id)).LinkedPurchaseReturnId!.Value;
            (await legacyDb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stock_movements SET total_cost = {legacy} WHERE source_doc_id = {returnId} AND quantity < 0"))
                .Should().Be(1);
        }
        if (cancel)
        {
            await using var db = CreateContext();
            var cancelled = await UnifiedCancelHandler(db).Handle(new(id, "Cancelación interna de prueba", Guid.NewGuid()), default);
            cancelled.IsSuccess.Should().BeTrue(cancelled.Error);
        }
        return (command, id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnifiedReprocessing_Complete_reversal_allows_one_new_active_note_preserving_history(bool discount)
    {
        var (command, oldId) = await CancelledNoteForReprocessing(discount);
        await using var db = CreateContext();
        var created = await UnifiedDraftHandler(db, ReprocessingGuard(db)).Handle(command with { ClientRequestId = Guid.NewGuid() }, default);
        created.IsSuccess.Should().BeTrue(created.Error);
        created.Value!.Id.Should().NotBe(oldId);
        var duplicate = await UnifiedDraftHandler(db, ReprocessingGuard(db)).Handle(command with { ClientRequestId = Guid.NewGuid() }, default);
        duplicate.IsSuccess.Should().BeFalse();
        (await db.PurchaseCreditNotes.CountAsync()).Should().Be(2);
        (await db.PurchaseCreditNotes.SingleAsync(n => n.Id == oldId)).Status.Should().Be(PurchaseCreditNoteStatus.Cancelled);
    }

    /// <summary>INVENTORY-V1-B3 — 3 × 10.00 + flete 0.01: costo 10.003333; la devolución total vale 30.01.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(30.009999)]
    public async Task UnifiedReprocessing_Fractional_inventory_cost_reversal_allows_reprocessing(double? legacyExitTotalCost)
    {
        var (command, oldId) = await CancelledNoteForReprocessing(unitPrice: 10m, quantity: 3m, paidAmount: 0m,
            freightCost: 0.01m, legacyExitTotalCost: (decimal?)legacyExitTotalCost);
        await using var db = CreateContext();
        var created = await UnifiedDraftHandler(db, ReprocessingGuard(db)).Handle(command with { ClientRequestId = Guid.NewGuid() }, default);
        created.IsSuccess.Should().BeTrue(created.Error);
        created.Value!.Id.Should().NotBe(oldId);
    }

    [Theory]
    [InlineData(SriFiscalStatus.Annulled)]
    [InlineData(SriFiscalStatus.PendingAnnulment)]
    [InlineData(SriFiscalStatus.Unknown)]
    public async Task UnifiedReprocessing_SRI_non_current_or_unverifiable_blocks(SriFiscalStatus status)
    {
        var (command, _) = await CancelledNoteForReprocessing();
        await using var db = CreateContext();
        var result = await UnifiedDraftHandler(db, ReprocessingGuard(db, status)).Handle(command with { ClientRequestId = Guid.NewGuid() }, default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("SRI");
        (await db.PurchaseCreditNotes.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("payable")]
    [InlineData("inventory")]
    [InlineData("accounting")]
    public async Task UnifiedReprocessing_Incomplete_reversal_blocks_without_new_documents(string incomplete)
    {
        var (command, id) = await CancelledNoteForReprocessing();
        await using (var broken = CreateContext())
        {
            var unitOfWork = new UnitOfWork(broken);
            await unitOfWork.BeginTransactionAsync();
            var note = await broken.PurchaseCreditNotes.SingleAsync(n => n.Id == id);
            if (incomplete == "payable")
            {
                var payable = await broken.Set<AccountsPayable>().Include(p => p.Installments)
                    .SingleAsync(p => p.OriginId == note.PurchaseInvoiceId);
                payable.ApplyCreditNote(1m, _userId);
            }
            else if (incomplete == "inventory")
            {
                var stock = new StockRepository(broken, new FixedCurrentCompany(() => _companyId),
                    new RealDatabaseExceptionTranslator(), StandardPrecisionPolicyProvider.Instance);
                var source = await broken.PurchaseReturns.Include(r => r.Lines).SingleAsync(r => r.Id == note.LinkedPurchaseReturnId);
                await stock.AppendMovementAsync(_tenantId, _companyId, _itemId, _warehouseId, StockMovementType.PurchaseReturn,
                    1m, "UNIT", DateOnly.FromDateTime(DateTime.UtcNow), "Reversa incorrecta (fixture aislada)",
                    source.Id, "PurchaseReturn", _userId, 100m, sourceDocLineId: source.Lines.Single().Id);
            }
            else
            {
                broken.AccountingPostings.Add(AccountingPosting.CreatePending(_tenantId, _companyId, "Purchases", "PurchaseReturn",
                    note.LinkedPurchaseReturnId!.Value, "NC test", command.IssueDate, command.IssueDate, 1, "{}", _userId, null));
            }
            await broken.SaveChangesAsync();
            await unitOfWork.CommitAsync();
        }
        await using var db = CreateContext();
        var result = await UnifiedDraftHandler(db, ReprocessingGuard(db)).Handle(command with { ClientRequestId = Guid.NewGuid() }, default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Reprocesamiento bloqueado");
        (await db.PurchaseCreditNotes.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UnifiedReprocessing_Unreversed_supplier_credit_blocks()
    {
        var (command, id) = await CancelledNoteForReprocessing(cancel: false);
        await using (var broken = CreateContext())
        {
            var unitOfWork = new UnitOfWork(broken);
            await unitOfWork.BeginTransactionAsync();
            var note = await broken.PurchaseCreditNotes.SingleAsync(n => n.Id == id);
            var source = await broken.PurchaseReturns.Include(r => r.Lines).SingleAsync(r => r.Id == note.LinkedPurchaseReturnId);
            var stock = new StockRepository(broken, new FixedCurrentCompany(() => _companyId), new RealDatabaseExceptionTranslator(), StandardPrecisionPolicyProvider.Instance);
            var exit = (await stock.GetMovementsByDocumentAsync(_tenantId, source.Id, "PurchaseReturn")).Single();
            await stock.AppendMovementAsync(_tenantId, _companyId, _itemId, _warehouseId, StockMovementType.PurchaseReturn,
                -exit.Quantity, exit.UomCode, exit.EffectiveDate, "Reversa parcial (fixture aislada)", source.Id, "PurchaseReturn", _userId,
                exit.UnitCost, sourceDocLineId: source.Lines.Single().Id);
            var payable = await broken.Set<AccountsPayable>().Include(p => p.Installments).SingleAsync(p => p.OriginId == source.PurchaseInvoiceId);
            payable.ReverseReturnCredit(source.AppliedToPayableAmount!.Value, _userId);
            source.Cancel("Fixture: falta reversa de SupplierCredit", _userId, Guid.NewGuid(), "hash");
            note.CancelLinkedReturn(source, _userId);
            await broken.SaveChangesAsync();
            await unitOfWork.CommitAsync();
        }
        await using var db = CreateContext();
        var result = await UnifiedDraftHandler(db, ReprocessingGuard(db)).Handle(command with { ClientRequestId = Guid.NewGuid() }, default);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("SupplierCredit");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnifiedReprocessing_Posted_accounting_requires_a_published_mirror(bool publishedMirror)
    {
        var (command, id) = await CancelledNoteForReprocessing();
        await using (var setup = CreateContext())
        {
            var note = await setup.PurchaseCreditNotes.SingleAsync(n => n.Id == id);
            var date = command.IssueDate;
            var period = AccountingPeriod.Create(_tenantId, _companyId, date.Year, 1,
                new DateOnly(date.Year, 1, 1), new DateOnly(date.Year, 12, 31), _userId);
            var counter = Account.Create(_tenantId, _companyId, AccountCode.Create("2.1.99.001"), "Contrapartida test",
                null, AccountType.Liability, AccountNature.Credit, true, _userId);
            setup.AccountingPeriods.Add(period);
            setup.Accounts.Add(counter);
            await setup.SaveChangesAsync();
            var original = JournalEntry.Create(_tenantId, _companyId, date, period.Id, date.Year,
                "Purchases", "PurchaseReturn", note.LinkedPurchaseReturnId!.Value, "Asiento histórico de prueba", _userId);
            original.AddLine(_accountId, null, 100m, 0m);
            original.AddLine(counter.Id, null, 0m, 100m);
            original.Post(_userId, 1);
            setup.JournalEntries.Add(original);
            await setup.SaveChangesAsync();
            if (publishedMirror)
                setup.JournalEntries.Add(original.Reverse(_userId, 2, "Reversa de NC", date, period.Id, date.Year));
            else
                setup.AccountingPostings.Add(AccountingPosting.CreatePending(_tenantId, _companyId,
                    "Accounting", "Reversal", original.Id, "Reversa pendiente", date, date, 1, "{}", _userId, null));
            await setup.SaveChangesAsync();
        }
        await using var db = CreateContext();
        var result = await UnifiedDraftHandler(db, ReprocessingGuard(db)).Handle(command with { ClientRequestId = Guid.NewGuid() }, default);
        result.IsSuccess.Should().Be(publishedMirror, result.Error);
        if (!publishedMirror) result.Error.Should().Contain("contable");
    }

    private sealed class ReprocessingSriQuery(SriFiscalStatus status) : ISriDocumentStatusQuery
    {
        public Task<SriDocumentStatusResult> QueryAsync(string accessKey, string wsdlUrl, CancellationToken ct = default) =>
            Task.FromResult(new SriDocumentStatusResult { Outcome = status == SriFiscalStatus.Unknown
                ? SriStatusQueryOutcome.Unavailable : SriStatusQueryOutcome.Success,
                FiscalStatus = status, AccessKey = accessKey, DocumentType = "04", IssuerRuc = "1791352688001" });
    }
}
