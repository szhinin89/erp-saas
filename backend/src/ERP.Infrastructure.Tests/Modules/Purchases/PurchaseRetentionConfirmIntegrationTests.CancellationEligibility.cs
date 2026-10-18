using ERP.Application.Common;
using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Catalogs;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    private StockRepository CancellationStock(ErpDbContext db) => new(db,
        new FixedCurrentCompany(_companyId), new PostgresDatabaseExceptionTranslator(),
        StandardPrecisionPolicyProvider.Instance);

    private async Task AppendCancellationScenarioAsync(decimal quantity, StockMovementType type, decimal? cost = null)
    {
        await using var db = CreateContext();
        var stock = CancellationStock(db);
        await stock.AppendMovementAsync(_tenantId, _companyId, _itemId, _warehouseId,
            type, quantity, "UNIT", IssueDate.AddDays(-1), "Cancellation eligibility test",
            Guid.NewGuid(), "StockAdjustment", _userId, cost);
        await stock.SaveChangesWithSequenceRetryAsync();
    }

    private async Task<string> CancellationPersistenceSnapshotAsync(Guid invoiceId)
    {
        await using var db = CreateContext();
        var invoice = await db.PurchaseInvoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
        var payables = await db.AccountsPayables.AsNoTracking().Include(p => p.Installments)
            .Where(p => p.OriginId == invoiceId).ToListAsync();
        var stock = await db.Set<CurrentStock>().AsNoTracking()
            .Where(s => s.ProductId == _itemId && s.WarehouseId == _warehouseId)
            .Select(s => new { s.Quantity, s.AverageCost, s.TotalStockValue }).ToListAsync();
        var journal = await db.JournalEntries.AsNoTracking().OrderBy(e => e.Id)
            .Select(e => new { e.Id, e.Status, e.SourceEventId }).ToListAsync();
        var movements = await db.Set<StockMovement>().AsNoTracking().OrderBy(m => m.SequenceNumber)
            .Select(m => new { m.Id, m.Quantity, m.TotalCost, m.RunningStockValue }).ToListAsync();
        return System.Text.Json.JsonSerializer.Serialize(new { invoice.Status, stock, journal, movements,
            payables = payables.Select(p => new { p.Id, p.Status, p.TotalAmount, p.PaidAmount,
                p.RetainedAmount, p.OutstandingAmount, p.SupplierCreditAmount, p.CreditNoteAmount }) });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationEligibility_MixedCosts_ExitBlocksPermanently_Atomic(bool reverseExit)
    {
        var a = await SeedDraftPurchaseAsync(quantity: 10m, unitPrice: 2m);
        (await ConfirmAsync(a, null)).IsSuccess.Should().BeTrue();
        var b = await SeedDraftPurchaseAsync(quantity: 1m, unitPrice: 10m);
        (await ConfirmAsync(b, null)).IsSuccess.Should().BeTrue();
        await AppendCancellationScenarioAsync(-8m, StockMovementType.NegativeAdjust);

        // Reproduce el fallo anterior exactamente: salida al costo histórico de B ($10).
        await using (var legacyDb = CreateContext())
        {
            var legacy = CancellationStock(legacyDb);
            var act = async () => await legacy.AppendMovementAsync(_tenantId, _companyId,
                _itemId, _warehouseId, StockMovementType.PurchaseCancelled, -1m, "UNIT",
                IssueDate, "Legacy repro", b, "PurchaseInvoice", _userId, 10m);
            await act.Should().ThrowAsync<DomainRuleViolationException>()
                .WithMessage("Inventory cost cannot be negative.");
        }
        if (reverseExit) await AppendCancellationScenarioAsync(8m, StockMovementType.PositiveAdjust, 30m / 11m);
        var before = await CancellationPersistenceSnapshotAsync(b);
        var result = await CancelPurchaseAsync(b);
        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Purchases.CancellationSubsequentInventoryExit);
        result.Error.Should().Contain("Utilice devolución o nota de crédito");
        (await CancellationPersistenceSnapshotAsync(b)).Should().Be(before);
        (await ReadAsync(b)).Status.Should().Be(PurchaseStatus.Confirmed);
    }

    [Fact]
    public async Task CancellationEligibility_LaterEntryAlone_AllowsHistoricalReversal()
    {
        var a = await SeedDraftPurchaseAsync(quantity: 10m, unitPrice: 2m);
        (await ConfirmAsync(a, null)).IsSuccess.Should().BeTrue();
        var b = await SeedDraftPurchaseAsync(quantity: 1m, unitPrice: 10m);
        (await ConfirmAsync(b, null)).IsSuccess.Should().BeTrue();
        (await CancelPurchaseAsync(a)).IsSuccess.Should().BeTrue();
        await using var db = CreateContext();
        var stock = await db.Set<CurrentStock>().SingleAsync(s => s.ProductId == _itemId);
        stock.Quantity.Should().Be(1m);
        stock.TotalStockValue.Should().Be(10m);
        var payable = await db.AccountsPayables.SingleAsync(p => p.OriginId == a);
        payable.Status.Should().Be(ERP.Domain.Modules.Payables.Enums.AccountsPayableStatus.Cancelled);
        var original = await db.JournalEntries.SingleAsync(e => e.SourceEventId == a
            && e.SourceModule == "Purchases" && e.SourceEventType == "InvoiceReceived");
        original.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.JournalEntryStatus.Posted);
        (await db.JournalEntries.CountAsync(e => e.SourceEventType == "Reversal"
            && e.SourceEventId == original.Id)).Should().Be(1);
    }

    [Fact]
    public async Task CancellationEligibility_GlobalSriPolicy_VersionsAndBusinessDayExtension()
    {
        await using var db = CreateContext();
        var resolver = new SriCatalogResolver(db);
        var legacy = await resolver.ResolveAnnulmentPolicyAsync(new DateOnly(2025, 7, 31));
        legacy.DaysAfterIssue.Should().Be(90);
        legacy.OrdinaryDeadline(new DateOnly(2025, 7, 31)).Should().Be(new DateOnly(2025, 10, 29));
        var current = await resolver.ResolveAnnulmentPolicyAsync(new DateOnly(2025, 8, 1));
        current.DayOfFollowingMonth.Should().Be(7);
        current.ExtendToNextBusinessDay.Should().BeTrue();
        current.OrdinaryDeadline(new DateOnly(2026, 12, 31)).Should().Be(new DateOnly(2027, 1, 7));
        // Calendario controlado: 7 sábado, 8 domingo y 9 feriado local; no inventa un calendario productivo.
        current.Deadline(new DateOnly(2026, 10, 1), d => d >= new DateOnly(2026, 11, 10))
            .Should().Be(new DateOnly(2026, 11, 10));
        var missing = async () => await resolver.ResolveAnnulmentPolicyAsync(new DateOnly(2016, 2, 21));
        await missing.Should().ThrowAsync<DomainRuleViolationException>();
        // No solapamiento del seed y fail-closed ante una versión normativa ambigua.
        (await db.Set<ERP.Domain.Modules.SriCatalogs.Entities.SriAnnulmentPolicyVersion>().CountAsync()).Should().Be(2);
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Add(new ERP.Domain.Modules.SriCatalogs.Entities.SriAnnulmentPolicyVersion
        {
            Id = Guid.NewGuid(), PolicyId = current.PolicyId, ValidFrom = new DateOnly(2026, 1, 1),
            DayOfFollowingMonth = 5, NormativeSourceId = current.NormativeSourceId,
        });
        await db.SaveChangesAsync();
        var ambiguous = async () => await resolver.ResolveAnnulmentPolicyAsync(new DateOnly(2026, 1, 1));
        await ambiguous.Should().ThrowAsync<DomainRuleViolationException>();
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task CancellationEligibility_ConcurrentExit_CommitsBeforeCheckUnderSameResourceLock()
    {
        var invoice = await SeedDraftPurchaseAsync(quantity: 10m, unitPrice: 2m);
        (await ConfirmAsync(invoice, null)).IsSuccess.Should().BeTrue();
        await using var writer = CreateContext();
        await using var tx = await writer.Database.BeginTransactionAsync();
        var stock = CancellationStock(writer);
        await stock.AppendMovementAsync(_tenantId, _companyId, _itemId, _warehouseId,
            StockMovementType.TransferExit, -1m, "UNIT", IssueDate, "Concurrent exit",
            Guid.NewGuid(), "StockTransfer", _userId);
        await stock.SaveChangesWithSequenceRetryAsync();
        var cancellation = CancelPurchaseAsync(invoice);
        // Esperar evidencia PostgreSQL de que Cancel está esperando el advisory lock, no un timing supuesto.
        await using var observer = new Npgsql.NpgsqlConnection(_postgres.GetConnectionString());
        await observer.OpenAsync();
        var waiting = false;
        for (var i = 0; i < 100 && !waiting; i++)
        {
            await using var query = new Npgsql.NpgsqlCommand(
                "SELECT EXISTS(SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND NOT granted)", observer);
            waiting = (bool)(await query.ExecuteScalarAsync())!;
            if (!waiting) await Task.Delay(30);
        }
        try
        {
            waiting.Should().BeTrue("Cancel debe esperar al escritor sobre la misma clave de inventario");
            cancellation.IsCompleted.Should().BeFalse();
        }
        finally { await tx.CommitAsync(); }
        var before = await CancellationPersistenceSnapshotAsync(invoice);
        var result = await cancellation.WaitAsync(TimeSpan.FromSeconds(30));
        result.Code.Should().Be(ApiResponseCodes.Purchases.CancellationSubsequentInventoryExit);
        (await CancellationPersistenceSnapshotAsync(invoice)).Should().Be(before);
    }

    [Fact]
    public async Task CancellationEligibility_ExitBeforePurchase_DoesNotBlock_AndScopeIsExact()
    {
        await AppendCancellationScenarioAsync(10m, StockMovementType.PositiveAdjust, 2m);
        await AppendCancellationScenarioAsync(-1m, StockMovementType.NegativeAdjust);
        var invoice = await SeedDraftPurchaseAsync(quantity: 1m, unitPrice: 10m);
        (await ConfirmAsync(invoice, null)).IsSuccess.Should().BeTrue();
        await using (var db = CreateContext())
        {
            var stock = CancellationStock(db);
            var entry = (await stock.GetMovementsByDocumentAsync(_tenantId, invoice, "PurchaseInvoice")).Single();
            (await stock.HasExitAfterAsync(_tenantId, _companyId, _itemId, _warehouseId, entry.SequenceNumber)).Should().BeFalse();
            (await stock.HasExitAfterAsync(Guid.NewGuid(), _companyId, _itemId, _warehouseId, 0)).Should().BeFalse();
            (await stock.HasExitAfterAsync(_tenantId, Guid.NewGuid(), _itemId, _warehouseId, 0)).Should().BeFalse();
            (await stock.HasExitAfterAsync(_tenantId, _companyId, Guid.NewGuid(), _warehouseId, 0)).Should().BeFalse();
            (await stock.HasExitAfterAsync(_tenantId, _companyId, _itemId, Guid.NewGuid(), 0)).Should().BeFalse();
        }
        (await CancelPurchaseAsync(invoice)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CancellationEligibility_AppliedCreditNote_BlocksWithoutPartialReversals()
    {
        var invoice = await SeedDraftPurchaseAsync();
        (await ConfirmAsync(invoice, null)).IsSuccess.Should().BeTrue();
        await using (var db = CreateContext())
        {
            var payable = await db.AccountsPayables.Include(p => p.Installments)
                .SingleAsync(p => p.OriginId == invoice);
            payable.ApplyCreditNote(10m, _userId);
            await db.SaveChangesAsync();
        }
        var before = await CancellationPersistenceSnapshotAsync(invoice);
        var result = await CancelPurchaseAsync(invoice);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("nota de crédito aplicada");
        (await CancellationPersistenceSnapshotAsync(invoice)).Should().Be(before);
    }
}
