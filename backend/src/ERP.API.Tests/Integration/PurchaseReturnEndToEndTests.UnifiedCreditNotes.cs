using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Events;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Tests.Integration;

public sealed partial class PurchaseReturnEndToEndTests
{
    private CreateDraftPurchaseCreditNoteHandler UnifiedDraftHandler(ErpDbContext db,
        PurchaseCreditNoteReprocessingGuard? guard = null) => new(
        new PurchaseCreditNoteRepository(db, new FixedCurrentCompany(() => _companyId)),
        new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
        new AccountsPayableRepository(db),
        new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(() => _companyId)),
        new RealDatabaseExceptionTranslator(), new FixedCurrentTenant(() => _tenantId),
        new FixedCurrentCompany(() => _companyId), new FixedCurrentBranch(() => _branchId),
        new FixedCurrentUser(_userId), StandardPrecisionPolicyProvider.Instance,
        new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)), guard);

    private AuthorizePurchaseCreditNoteHandler UnifiedAuthorizeHandler(ErpDbContext db) => new(
        new PurchaseCreditNoteRepository(db, new FixedCurrentCompany(() => _companyId)),
        new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
        new AccountsPayableRepository(db),
        new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
        new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(() => _companyId)),
        new UnitOfWork(db), new RealDatabaseExceptionTranslator(), new FixedCurrentTenant(() => _tenantId),
        new FixedCurrentBranch(() => _branchId), new FixedCurrentUser(_userId),
        new PurchaseCreditNoteReturnMotor(BuildAuthorizeHandler(db, true), BuildCancelReturnHandler(db, true)));

    private static CreateDraftPurchaseCreditNoteCommand UnifiedReturnCommand(SeededInvoice invoice,
        Guid? historicalReturnId = null) => new(
            Guid.NewGuid(), invoice.InvoiceId, null, PurchaseCreditNoteApplicationType.Return,
            $"001-001-{Random.Shared.Next(100000000, 999999999)}", null, null, null,
            DateOnly.FromDateTime(DateTime.UtcNow), "Devolución con NC", [], [],
            historicalReturnId is null ? [new(invoice.LineId, 3m)] : [], historicalReturnId);

    private CancelPurchaseCreditNoteHandler UnifiedCancelHandler(ErpDbContext db) => new(
        new PurchaseCreditNoteRepository(db, new FixedCurrentCompany(() => _companyId)),
        new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)), new AccountsPayableRepository(db),
        new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
        new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(() => _companyId)),
        new UnitOfWork(db), new RealDatabaseExceptionTranslator(), new FixedCurrentTenant(() => _tenantId),
        new FixedCurrentUser(_userId),
        new PurchaseCreditNoteReturnMotor(BuildAuthorizeHandler(db, true), BuildCancelReturnHandler(db, true)));

    [Fact]
    public async Task UnifiedCreditNote_Cancel_return_from_NC_reverses_once_and_preserves_documents()
    {
        var invoice = await SeedConfirmedInvoiceAsync(100m, 10m);
        Guid noteId;
        await using (var db = CreateContext())
        {
            var created = await UnifiedDraftHandler(db).Handle(UnifiedReturnCommand(invoice), default);
            noteId = created.Value!.Id;
        }
        await using (var db = CreateContext())
            (await UnifiedAuthorizeHandler(db).Handle(new(noteId, Guid.NewGuid()), default)).IsSuccess.Should().BeTrue();
        var requestId = Guid.NewGuid();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var db = CreateContext();
            var cancelled = await UnifiedCancelHandler(db).Handle(new(noteId, "NC anulada", requestId), default);
            cancelled.IsSuccess.Should().BeTrue(cancelled.Error);
            cancelled.Value!.Status.Should().Be("Cancelled");
        }
        await using var verify = CreateContext();
        (await verify.PurchaseReturns.SingleAsync()).Status.Should().Be(PurchaseReturnStatus.Cancelled);
        (await verify.PurchaseCreditNotes.SingleAsync()).Status.Should().Be(PurchaseCreditNoteStatus.Cancelled);
        (await verify.Set<StockMovement>().CountAsync()).Should().Be(3, "entrada, salida y una sola reversa");
        var payable = await verify.Set<AccountsPayable>().Include(p => p.Installments).SingleAsync(p => p.Id == invoice.PayableId);
        payable.OutstandingAmount.Should().Be(1120m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public async Task UnifiedCreditNote_Return_authorizes_once_with_payable_and_supplier_credit(decimal paid)
    {
        var invoice = await SeedConfirmedInvoiceAsync(100m, 10m, paid);
        Guid noteId;
        Guid returnId;
        var command = UnifiedReturnCommand(invoice);
        await using (var db = CreateContext())
        {
            var created = await UnifiedDraftHandler(db).Handle(command, default);
            created.IsSuccess.Should().BeTrue(created.Error);
            noteId = created.Value!.Id;
            returnId = created.Value.LinkedPurchaseReturnId!.Value;
        }
        var notifications = new UnifiedNotifications();
        var requestId = Guid.NewGuid();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var db = CreateContext(notifications);
            var result = await UnifiedAuthorizeHandler(db).Handle(new(noteId, requestId), default);
            result.IsSuccess.Should().BeTrue(result.Error);
            result.Value!.Status.Should().Be("Authorized");
        }
        await using var verify = CreateContext();
        (await verify.PurchaseReturns.CountAsync()).Should().Be(1);
        (await verify.Set<StockMovement>().CountAsync(m => m.SourceDocId == returnId
            && m.MovementType == StockMovementType.PurchaseReturn)).Should().Be(1);
        var payable = await verify.Set<AccountsPayable>().Include(p => p.Installments).SingleAsync(p => p.Id == invoice.PayableId);
        payable.OutstandingAmount.Should().Be(paid == 0 ? 784m : 0m);
        var credit = await verify.Set<SupplierCredit>().SingleOrDefaultAsync(c => c.SourcePurchaseReturnId == returnId);
        if (paid == 0) credit.Should().BeNull();
        else credit!.OriginalAmount.Should().Be(216m);
        notifications.Events.OfType<PurchaseReturnAuthorizedEvent>().Should().ContainSingle();
        notifications.Events.OfType<PurchaseCreditNoteAuthorizedEvent>().Should().BeEmpty("el asiento se origina solo en el motor de devolución");
        var persisted = await verify.PurchaseCreditNotes.SingleAsync(n => n.Id == noteId);
        persisted.AuthorizeClientRequestId.Should().Be(requestId);
    }

    [Fact]
    public async Task UnifiedCreditNote_Historical_link_has_no_new_physical_financial_or_posting_effects()
    {
        var invoice = await SeedConfirmedInvoiceAsync(100m, 10m, 1000m);
        var historical = await AuthorizeDraftAsync(invoice.InvoiceId, invoice.LineId, 3m);
        var before = await UnifiedSnapshot();
        var command = UnifiedReturnCommand(invoice, historical.Id);
        Guid noteId;
        await using (var db = CreateContext())
        {
            var candidate = await new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId))
                .GetPendingCreditNoteByInvoiceAsync(_tenantId, _branchId, invoice.InvoiceId);
            candidate.Should().ContainSingle();
            var created = await UnifiedDraftHandler(db).Handle(command, default);
            created.IsSuccess.Should().BeTrue(created.Error);
            noteId = created.Value!.Id;
        }
        var events = new UnifiedNotifications();
        var requestId = Guid.NewGuid();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var db = CreateContext(events);
            var result = await UnifiedAuthorizeHandler(db).Handle(new(noteId, requestId), default);
            result.IsSuccess.Should().BeTrue(result.Error);
        }
        (await UnifiedSnapshot()).Should().BeEquivalentTo(before);
        events.Events.OfType<PurchaseReturnAuthorizedEvent>().Should().BeEmpty();
        events.Events.OfType<PurchaseCreditNoteAuthorizedEvent>().Should().BeEmpty();
        await using var fresh = CreateContext();
        var duplicate = await UnifiedDraftHandler(fresh).Handle(command with { ClientRequestId = Guid.NewGuid() }, default);
        duplicate.IsSuccess.Should().BeFalse();
        var candidates = await new PurchaseReturnRepository(fresh, new FixedCurrentCompany(() => _companyId))
            .GetPendingCreditNoteByInvoiceAsync(_tenantId, _branchId, invoice.InvoiceId);
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task UnifiedCreditNote_Historical_link_rejects_other_invoice_and_duplicate_comprobante()
    {
        var invoice = await SeedConfirmedInvoiceAsync(100m, 10m);
        var other = await SeedConfirmedInvoiceAsync(100m, 10m);
        var historical = await AuthorizeDraftAsync(invoice.InvoiceId, invoice.LineId, 3m);
        await using (var db = CreateContext())
        {
            var wrongInvoice = await UnifiedDraftHandler(db).Handle(UnifiedReturnCommand(other, historical.Id), default);
            wrongInvoice.IsSuccess.Should().BeFalse();
        }
        var command = UnifiedReturnCommand(invoice, historical.Id);
        await using (var db = CreateContext())
        {
            var created = await UnifiedDraftHandler(db).Handle(command, default);
            created.IsSuccess.Should().BeTrue(created.Error);
            var retry = await UnifiedDraftHandler(db).Handle(command, default);
            retry.Value!.Id.Should().Be(created.Value!.Id);
        }
        await using var duplicateDb = CreateContext();
        var duplicate = await UnifiedDraftHandler(duplicateDb).Handle(command with {
            ClientRequestId = Guid.NewGuid(), HistoricalPurchaseReturnId = null,
            ReturnLines = [new(invoice.LineId, 1m)] }, default);
        duplicate.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UnifiedCreditNote_Concurrent_authorization_and_historical_reservation_do_not_duplicate()
    {
        var invoice = await SeedConfirmedInvoiceAsync(100m, 10m);
        var command = UnifiedReturnCommand(invoice);
        Guid noteId;
        await using (var db = CreateContext())
        {
            var created = await UnifiedDraftHandler(db).Handle(command, default);
            created.IsSuccess.Should().BeTrue(created.Error);
            noteId = created.Value!.Id;
        }
        var requestId = Guid.NewGuid();
        async Task Authorize() {
            await using var db = CreateContext();
            var result = await UnifiedAuthorizeHandler(db).Handle(new(noteId, requestId), default);
            result.IsSuccess.Should().BeTrue(result.Error);
        }
        await Task.WhenAll(Authorize(), Authorize());
        await using (var db = CreateContext())
        {
            (await db.Set<StockMovement>().CountAsync(m => m.MovementType == StockMovementType.PurchaseReturn)).Should().Be(1);
        }
        var historicalInvoice = await SeedConfirmedInvoiceAsync(100m, 10m);
        var historical = await AuthorizeDraftAsync(historicalInvoice.InvoiceId, historicalInvoice.LineId, 3m);
        async Task<bool> Reserve() {
            await using var db = CreateContext();
            var result = await UnifiedDraftHandler(db).Handle(UnifiedReturnCommand(historicalInvoice, historical.Id), default);
            return result.IsSuccess;
        }
        var outcomes = await Task.WhenAll(Reserve(), Reserve());
        outcomes.Count(success => success).Should().Be(1);
    }

    [Theory]
    [InlineData("currency")]
    [InlineData("supplier")]
    [InlineData("status")]
    [InlineData("amount")]
    public async Task UnifiedCreditNote_Historical_received_document_must_be_compatible(string mismatch)
    {
        var invoice = await SeedConfirmedInvoiceAsync(100m, 10m);
        var historical = await AuthorizeDraftAsync(invoice.InvoiceId, invoice.LineId, 3m);
        var before = await UnifiedSnapshot();
        Guid receptionId;
        await using (var db = CreateContext())
        {
            var receipt = PurchaseReceptionDocument.Create(_tenantId, _companyId, _branchId,
                PurchaseReceptionSourceDocType.CreditNote, mismatch == "supplier" ? "1799999999001" : "1791352688001",
                "Proveedor E2E", _supplierId, new string('8', 49), "001-001-000000099",
                DateOnly.FromDateTime(DateTime.UtcNow), null, 300m, 36m, mismatch == "amount" ? 400m : 336m,
                _userId, currencyCode: mismatch == "currency" ? "EUR" : "USD");
            if (mismatch != "status") receipt.MarkVerified(_userId);
            db.PurchaseReceptionDocuments.Add(receipt);
            await db.SaveChangesAsync();
            receptionId = receipt.Id;
        }
        Guid? noteId = null;
        await using (var db = CreateContext())
        {
            var created = await UnifiedDraftHandler(db).Handle(UnifiedReturnCommand(invoice, historical.Id) with {
                ReceptionDocumentId = receptionId }, default);
            if (created.IsSuccess) noteId = created.Value!.Id;
            else mismatch.Should().Be("amount");
        }
        if (noteId is { } id)
        {
            await using var db = CreateContext();
            var action = () => UnifiedAuthorizeHandler(db).Handle(new(id, Guid.NewGuid()), default);
            await action.Should().ThrowAsync<ERP.Domain.Exceptions.DomainRuleViolationException>();
        }
        (await UnifiedSnapshot()).Should().BeEquivalentTo(before);
    }

    private async Task<object> UnifiedSnapshot()
    {
        await using var db = CreateContext();
        return new {
            Returns = await db.PurchaseReturns.CountAsync(),
            Movements = await db.Set<StockMovement>().CountAsync(),
            Credits = await db.Set<SupplierCredit>().AsNoTracking().Select(c => new { c.Id, c.OriginalAmount }).ToListAsync(),
            Payables = await db.Set<AccountsPayable>().Include(p => p.Installments).AsNoTracking()
                .Select(p => new { p.Id, p.ReturnCreditAmount, p.CreditNoteAmount, p.SupplierCreditAmount }).ToListAsync(),
            Postings = await db.Set<ERP.Domain.Modules.Accounting.Entities.AccountingPosting>().CountAsync(),
            Journals = await db.Set<ERP.Domain.Modules.Accounting.Entities.JournalEntry>().CountAsync(),
        };
    }

    private sealed class UnifiedNotifications : IPublisher
    {
        public List<object> Events { get; } = [];
        public Task Publish(object notification, CancellationToken cancellationToken = default) {
            Events.Add(notification); return Task.CompletedTask;
        }
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Publish((object)notification, cancellationToken);
    }
}
