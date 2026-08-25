using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// ACCOUNTING-INVENTORY-COGS-07 — costo de ventas (SalesInvoiceCogsPostingTranslator) y su
/// reverso en devolución (SalesReturnCogsReversalPostingTranslator), a través del pipeline REAL
/// (Translator → PostingEngine real, con repositorios mockeados) — mismo patrón que
/// <see cref="CollectionAndSupplierPaymentPostingPipelineTests"/>. El costo siempre viene de
/// <c>StockMovement.TotalCost</c> (Kardex) — nunca de <c>GrandTotal</c>/precio de venta, por
/// diseño: el evento de venta ni siquiera carga un monto de costo.
/// </summary>
public sealed class SalesInvoiceCogsPostingPipelineTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    // GrandTotal (precio de venta, 500) es deliberadamente distinto del costo Kardex (180) — si
    // el translator alguna vez recalculara desde el precio de venta, este test lo detectaría.
    private static SalesInvoiceAuthorizedEvent InvoiceEvent(Guid invoiceId, decimal grandTotal = 500m) =>
        new(
            invoiceId,
            "001-001-000000001",
            grandTotal,
            CreatedBy,
            Guid.NewGuid(),
            TenantId,
            CompanyId,
            new DateOnly(2026, 8, 15),
            grandTotal / 1.15m,
            grandTotal - grandTotal / 1.15m,
            0m,
            0m
        );

    private static SalesReturnAuthorizedEvent ReturnEvent(Guid returnId, decimal grandTotal = 250m) =>
        new(
            returnId,
            Guid.NewGuid(),
            "DEV-000001",
            grandTotal,
            CreatedBy,
            TenantId,
            CompanyId,
            grandTotal / 1.15m,
            grandTotal - grandTotal / 1.15m,
            0m,
            0m,
            "Producto en mal estado"
        );

    private static StockMovement SaleExitMovement(Guid invoiceId, decimal totalCost, long seq = 1)
    {
        var unitCost = totalCost / 10m;
        return StockMovement.Create(
            TenantId,
            BranchId,
            ProductId,
            WarehouseId,
            StockMovementType.SaleExit,
            -10m,
            "UNI",
            50m,
            seq,
            unitCost,
            (50m - 10m) * unitCost,
            new DateOnly(2026, 8, 15),
            null,
            invoiceId,
            "SalesInvoice",
            CreatedBy,
            CompanyId,
            unitCost
        );
    }

    private static StockMovement SaleReturnMovement(Guid returnId, decimal totalCost, long seq = 1)
    {
        var unitCost = totalCost / 5m;
        return StockMovement.Create(
            TenantId,
            BranchId,
            ProductId,
            WarehouseId,
            StockMovementType.SaleReturn,
            5m,
            "UNI",
            40m,
            seq,
            unitCost,
            (40m + 5m) * unitCost,
            new DateOnly(2026, 8, 20),
            null,
            returnId,
            "SalesReturn",
            CreatedBy,
            CompanyId,
            unitCost
        );
    }

    private static Account PostableAccount(string code, string name) =>
        Account.Create(
            TenantId,
            CompanyId,
            AccountCode.Create(code),
            name,
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            CreatedBy
        );

    private static AccountingPeriod OpenPeriod() =>
        AccountingPeriod.Create(
            TenantId,
            CompanyId,
            2026,
            8,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            CreatedBy
        );

    private static PostingRule Rule(string factType, Guid debitAccountId, Guid creditAccountId)
    {
        var rule = PostingRule.Create(TenantId, CompanyId, "Sales", factType, null, null, null, CreatedBy);
        rule.AddLine(debitAccountId, AccountNature.Debit, PostingAmountKind.HistoricalCost);
        rule.AddLine(creditAccountId, AccountNature.Credit, PostingAmountKind.HistoricalCost);
        return rule;
    }

    private sealed class Mocks
    {
        public Mock<IJournalEntryRepository> JournalEntries { get; } = new();
        public Mock<IPostingRuleRepository> PostingRules { get; } = new();
        public Mock<IAccountingPeriodRepository> AccountingPeriods { get; } = new();
        public Mock<IJournalEntrySequenceRepository> JournalEntrySequences { get; } = new();
        public Mock<IAccountRepository> Accounts { get; } = new();
        public Mock<IStockRepository> Stock { get; } = new();
        public JournalEntry? Captured { get; private set; }

        public Mocks()
        {
            JournalEntries
                .Setup(r =>
                    r.AcquireIdempotencyLockAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(Task.CompletedTask);
            JournalEntries
                .Setup(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
                .Callback<JournalEntry, CancellationToken>((e, _) => Captured = e)
                .Returns(Task.CompletedTask);
            AccountingPeriods
                .Setup(r =>
                    r.FindContainingDateAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<DateOnly>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(OpenPeriod());
            JournalEntrySequences
                .Setup(r =>
                    r.ReserveNextNumberAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(1);
        }

        public void RegisterAccount(Account account) =>
            Accounts
                .Setup(r =>
                    r.GetByIdAsync(TenantId, CompanyId, account.Id, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(account);

        public PostingEngine BuildEngine() =>
            new(
                JournalEntries.Object,
                PostingRules.Object,
                AccountingPeriods.Object,
                JournalEntrySequences.Object,
                Accounts.Object,
                NullLogger<PostingEngine>.Instance
            );
    }

    [Fact]
    public async Task Venta_con_salida_de_inventario_genera_asiento_COGS_balanceado_con_el_costo_de_Kardex()
    {
        var m = new Mocks();
        var cogs = PostableAccount("5.1.01", "Costo de Ventas");
        var inventory = PostableAccount("1.1.03", "Inventario");
        m.RegisterAccount(cogs);
        m.RegisterAccount(inventory);
        var rule = Rule("CostOfGoodsSold", cogs.Id, inventory.Id);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "CostOfGoodsSold", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        var invoiceId = Guid.NewGuid();
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "CostOfGoodsSold", invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);
        // Costo Kardex = 180 — muy distinto del precio de venta (500/GrandTotal) del evento.
        m.Stock
            .Setup(r => r.GetMovementsByDocumentAsync(TenantId, invoiceId, "SalesInvoice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockMovement> { SaleExitMovement(invoiceId, 180m) });

        var engine = m.BuildEngine();
        var translator = new SalesInvoiceCogsPostingTranslator(m.Stock.Object, engine, NullLogger<SalesInvoiceCogsPostingTranslator>.Instance);

        await translator.Handle(InvoiceEvent(invoiceId, grandTotal: 500m), CancellationToken.None);

        m.Captured.Should().NotBeNull();
        var entry = m.Captured!;
        entry.SourceModule.Should().Be("Sales");
        entry.SourceEventType.Should().Be("CostOfGoodsSold");
        entry.SourceEventId.Should().Be(invoiceId);
        entry.Lines.Should().HaveCount(2);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        entry.Lines.Should().Contain(l => l.AccountId == cogs.Id && l.Debit == 180m && l.Credit == 0m);
        entry.Lines.Should().Contain(l => l.AccountId == inventory.Id && l.Credit == 180m && l.Debit == 0m);
        // El costo usado (180) nunca coincide con GrandTotal (500) — confirma que no se recalculó
        // desde el precio de venta.
        entry.Lines.Sum(l => l.Debit).Should().NotBe(500m);
    }

    [Fact]
    public async Task Venta_sin_movimientos_de_inventario_no_genera_ningun_asiento_de_costo()
    {
        var m = new Mocks();
        var invoiceId = Guid.NewGuid();
        m.Stock
            .Setup(r => r.GetMovementsByDocumentAsync(TenantId, invoiceId, "SalesInvoice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockMovement>()); // venta solo de servicios, sin inventario

        var engine = m.BuildEngine();
        var translator = new SalesInvoiceCogsPostingTranslator(m.Stock.Object, engine, NullLogger<SalesInvoiceCogsPostingTranslator>.Instance);

        await translator.Handle(InvoiceEvent(invoiceId), CancellationToken.None);

        m.Captured.Should().BeNull();
        m.JournalEntries.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        m.PostingRules.Verify(
            r => r.FindByKeyAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "sin costo que contabilizar, ni siquiera debe resolverse la PostingRule"
        );
    }

    [Fact]
    public async Task Evento_de_venta_duplicado_no_duplica_el_asiento_de_costo()
    {
        var m = new Mocks();
        var cogs = PostableAccount("5.1.01", "Costo de Ventas");
        var inventory = PostableAccount("1.1.03", "Inventario");
        m.RegisterAccount(cogs);
        m.RegisterAccount(inventory);
        var rule = Rule("CostOfGoodsSold", cogs.Id, inventory.Id);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "CostOfGoodsSold", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        var invoiceId = Guid.NewGuid();
        var existing = JournalEntry.Create(
            TenantId, CompanyId, new DateOnly(2026, 8, 15), Guid.NewGuid(), 2026,
            "Sales", "CostOfGoodsSold", invoiceId, "Costo ya contabilizado", CreatedBy
        );
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "CostOfGoodsSold", invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        m.Stock
            .Setup(r => r.GetMovementsByDocumentAsync(TenantId, invoiceId, "SalesInvoice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockMovement> { SaleExitMovement(invoiceId, 180m) });

        var engine = m.BuildEngine();
        var translator = new SalesInvoiceCogsPostingTranslator(m.Stock.Object, engine, NullLogger<SalesInvoiceCogsPostingTranslator>.Instance);

        await translator.Handle(InvoiceEvent(invoiceId), CancellationToken.None);

        m.Captured.Should().BeNull("el hecho ya contabilizado nunca debe generar un segundo asiento");
        m.JournalEntries.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cuenta_de_inventario_inactiva_falla_con_POSTING_ACCOUNT_INVALID_y_no_persiste()
    {
        var m = new Mocks();
        var cogs = PostableAccount("5.1.01", "Costo de Ventas");
        var inventory = PostableAccount("1.1.03", "Inventario");
        inventory.Disable(CreatedBy);
        m.RegisterAccount(cogs);
        m.RegisterAccount(inventory);
        var rule = Rule("CostOfGoodsSold", cogs.Id, inventory.Id);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "CostOfGoodsSold", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        var invoiceId = Guid.NewGuid();
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "CostOfGoodsSold", invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);
        m.Stock
            .Setup(r => r.GetMovementsByDocumentAsync(TenantId, invoiceId, "SalesInvoice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockMovement> { SaleExitMovement(invoiceId, 180m) });

        var engine = m.BuildEngine();
        var translator = new SalesInvoiceCogsPostingTranslator(m.Stock.Object, engine, NullLogger<SalesInvoiceCogsPostingTranslator>.Instance);

        await translator.Handle(InvoiceEvent(invoiceId), CancellationToken.None);

        m.Captured.Should().BeNull();
        m.JournalEntries.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cuenta_de_costo_AllowsPosting_false_falla_con_POSTING_ACCOUNT_INVALID_y_no_persiste()
    {
        var m = new Mocks();
        var cogs = Account.Create(
            TenantId, CompanyId, AccountCode.Create("5.1.02"), "Costo de Ventas (resumen)",
            null, AccountType.Expense, AccountNature.Debit, allowsPosting: false, CreatedBy
        );
        var inventory = PostableAccount("1.1.03", "Inventario");
        m.RegisterAccount(cogs);
        m.RegisterAccount(inventory);
        var rule = Rule("CostOfGoodsSold", cogs.Id, inventory.Id);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "CostOfGoodsSold", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        var invoiceId = Guid.NewGuid();
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "CostOfGoodsSold", invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);
        m.Stock
            .Setup(r => r.GetMovementsByDocumentAsync(TenantId, invoiceId, "SalesInvoice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockMovement> { SaleExitMovement(invoiceId, 180m) });

        var engine = m.BuildEngine();
        var translator = new SalesInvoiceCogsPostingTranslator(m.Stock.Object, engine, NullLogger<SalesInvoiceCogsPostingTranslator>.Instance);

        await translator.Handle(InvoiceEvent(invoiceId), CancellationToken.None);

        m.Captured.Should().BeNull();
        m.JournalEntries.Verify(r => r.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Devolucion_de_venta_genera_asiento_compensatorio_Debe_Inventario_Haber_Costo()
    {
        var m = new Mocks();
        var inventory = PostableAccount("1.1.03", "Inventario");
        var cogs = PostableAccount("5.1.01", "Costo de Ventas");
        m.RegisterAccount(inventory);
        m.RegisterAccount(cogs);
        // Reverso: Debe Inventario / Haber Costo — nature invertida respecto a la regla original.
        var rule = Rule("CostOfGoodsSoldReversed", inventory.Id, cogs.Id);
        m.PostingRules
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "CostOfGoodsSoldReversed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        var returnId = Guid.NewGuid();
        m.JournalEntries
            .Setup(r => r.FindByKeyAsync(TenantId, CompanyId, "Sales", "CostOfGoodsSoldReversed", returnId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);
        m.Stock
            .Setup(r => r.GetMovementsByDocumentAsync(TenantId, returnId, "SalesReturn", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockMovement> { SaleReturnMovement(returnId, 90m) });

        var engine = m.BuildEngine();
        var translator = new SalesReturnCogsReversalPostingTranslator(m.Stock.Object, engine, NullLogger<SalesReturnCogsReversalPostingTranslator>.Instance);

        await translator.Handle(ReturnEvent(returnId), CancellationToken.None);

        m.Captured.Should().NotBeNull();
        var entry = m.Captured!;
        entry.SourceModule.Should().Be("Sales");
        entry.SourceEventType.Should().Be("CostOfGoodsSoldReversed");
        entry.SourceEventId.Should().Be(returnId);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        entry.Lines.Should().Contain(l => l.AccountId == inventory.Id && l.Debit == 90m && l.Credit == 0m);
        entry.Lines.Should().Contain(l => l.AccountId == cogs.Id && l.Credit == 90m && l.Debit == 0m);
    }
}
