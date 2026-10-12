using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.CorrectMatch;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.Models;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Inventory.ItemMatching;

/// <summary>
/// FASE 3 — "Corregir relación" B→C: corrige la línea actual y sus hermanas del mismo código,
/// reemplaza la equivalencia futura (la anterior se deshabilita) y nunca toca compras confirmadas.
/// </summary>
public sealed class CorrectReceptionLineMatchHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IPurchaseReceptionDocumentRepository> Documents { get; } = new();
        public Mock<IPurchaseInvoiceRepository> Purchases { get; } = new();
        public Mock<IItemRepository> Items { get; } = new();
        public Mock<IItemMatchConfirmationService> Confirmation { get; } = new();
        public Mock<IPurchaseReceptionAutoMatcher> AutoMatcher { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public PurchaseReceptionDocument Document { get; }
        public PurchaseReceptionLine Line { get; }

        /// <summary>Mismo código que <see cref="Line"/>, vinculada al mismo producto equivocado.</summary>
        public PurchaseReceptionLine Sibling { get; }
        public PurchaseReceptionLine Other { get; }
        public Item Wrong { get; } = StockItem("B-1");
        public Item Right { get; } = StockItem("C-1");
        public Guid RightBoxId { get; }

        /// <summary>Dueño ACTIVO de la equivalencia Proveedor + COD-A (simula la tabla).</summary>
        public Guid? CodeOwner { get; set; }
        public bool CodeIsPrimaryOnOwner { get; set; }

        public Fixture()
        {
            Document = PurchaseReceptionDocument.Create(
                TenantId,
                CompanyId,
                BranchId,
                PurchaseReceptionSourceDocType.Invoice,
                "1791352688001",
                "Proveedor",
                SupplierId,
                new string('1', 49),
                "001-001-000000001",
                new DateOnly(2026, 9, 27),
                null,
                30m,
                0m,
                30m,
                UserId
            );
            Line = ReceptionLine("COD-A");
            Sibling = ReceptionLine("cod-a ");
            Other = ReceptionLine("COD-B");
            Document.AttachSriAuthorization(
                "1",
                DateTime.UtcNow,
                "<factura/>",
                DateTime.UtcNow,
                [Line, Sibling, Other],
                UserId,
                "01",
                null,
                new PurchaseReceptionProcessingOutcome(
                    PurchaseReceptionProcessingStatus.Processed,
                    3,
                    3,
                    null
                )
            );
            Line.ManualMatch(Wrong.Id, UserId, DateTime.UtcNow);
            Sibling.ManualMatch(Wrong.Id, UserId, DateTime.UtcNow);
            CodeOwner = Wrong.Id;
            RightBoxId = Right.PackagingLevels.Single(p => p.BaseQuantity == 12m).Id;

            foreach (var line in Document.Lines)
                Documents
                    .Setup(d => d.GetByLineIdAsync(TenantId, line.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Document);
            foreach (var item in new[] { Wrong, Right })
            {
                Items
                    .Setup(i => i.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(item);
                Items
                    .Setup(i => i.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(item);
            }
            Items
                .Setup(i =>
                    i.PackagingLevelBelongsToItemAsync(
                        Right.Id,
                        RightBoxId,
                        TenantId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(true);
            Items
                .Setup(i =>
                    i.SupplierCodeExistsAsync(
                        SupplierId,
                        "COD-A",
                        TenantId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(() => CodeOwner is not null);
            Items
                .Setup(i =>
                    i.GetSupplierCodeMatchAsync(
                        SupplierId,
                        It.Is<string>(c => c.Trim().ToUpperInvariant() == "COD-A"),
                        TenantId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(() =>
                    CodeOwner is { } owner
                        ? new ItemSupplierCodeMatch(
                            owner,
                            owner == Right.Id ? RightBoxId : null,
                            "CJ",
                            12m,
                            "UND"
                        )
                        : null
                );

            // Mismo efecto observable que ItemMatchConfirmationService: deshabilita (si no es
            // principal) / aprende la equivalencia y vincula o desvincula la línea.
            Confirmation
                .Setup(c =>
                    c.UnconfirmAsync(
                        Document,
                        It.IsAny<PurchaseReceptionLine>(),
                        UserId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback<PurchaseReceptionDocument, PurchaseReceptionLine, Guid, CancellationToken>(
                    (_, line, _, _) =>
                    {
                        if (!CodeIsPrimaryOnOwner)
                            CodeOwner = null;
                        line.UnmatchItem();
                    }
                )
                .Returns(Task.CompletedTask);
            Confirmation
                .Setup(c =>
                    c.ConfirmAsync(
                        Document,
                        It.IsAny<PurchaseReceptionLine>(),
                        It.IsAny<Guid>(),
                        UserId,
                        It.IsAny<DateTime>(),
                        It.IsAny<Guid?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Callback(
                    (
                        PurchaseReceptionDocument _,
                        PurchaseReceptionLine line,
                        Guid itemId,
                        Guid by,
                        DateTime at,
                        Guid? _,
                        CancellationToken _
                    ) =>
                    {
                        CodeOwner ??= itemId;
                        line.ManualMatch(itemId, by, at);
                    }
                )
                .Returns(Task.CompletedTask);
            UnitOfWork
                .Setup(u =>
                    u.ExecuteInTransactionAsync(
                        It.IsAny<Func<CancellationToken, Task>>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns<Func<CancellationToken, Task>, CancellationToken>((op, ct) => op(ct));
        }

        private PurchaseReceptionLine ReceptionLine(string code) =>
            PurchaseReceptionLine.Create(
                Document.Id,
                TenantId,
                $"XML {code}",
                1m,
                10m,
                "0",
                "2",
                0m,
                0m,
                0m,
                0m,
                10m,
                10m,
                supplierCode: code
            );

        public CorrectReceptionLineMatchHandler Build() =>
            new(
                Documents.Object,
                Purchases.Object,
                Items.Object,
                Confirmation.Object,
                AutoMatcher.Object,
                UnitOfWork.Object,
                Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
                Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId),
                Mock.Of<ICurrentBranch>(b => b.BranchId == BranchId),
                Mock.Of<ICurrentUser>(u => u.UserId == UserId),
                Mock.Of<IDatabaseExceptionTranslator>()
            );

        public Task<Result<CorrectReceptionLineMatchResultDto>> CorrectAsync(Guid? levelId = null) =>
            Build()
                .Handle(
                    new CorrectReceptionLineMatchCommand(Line.Id, Right.Id, levelId ?? RightBoxId),
                    CancellationToken.None
                );
    }

    private static Item StockItem(string sku)
    {
        var item = Item.Create(
            TenantId,
            sku,
            sku,
            sku,
            Guid.NewGuid(),
            "UND",
            ItemTaxConfig.Create("4", "4"),
            ItemSaleConfig.Create(true),
            ItemStockConfig.Create(true),
            UserId,
            companyId: CompanyId
        );
        item.ReplacePackagingLevels(
            [
                ("Unidad", 1, 1m, "UND", null, null, true, false, true),
                ("Caja x12", 2, 12m, "CJ", null, null, false, true, false),
            ],
            UserId
        );
        return item;
    }

    [Fact]
    public async Task Correcting_B_to_C_relinks_current_and_sibling_lines_and_replaces_future_equivalence()
    {
        var f = new Fixture();

        var result = await f.CorrectAsync();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines.Select(l => l.PurchaseReceptionLineId)
            .Should()
            .BeEquivalentTo([f.Line.Id, f.Sibling.Id]);
        result.Value.Lines.Should().OnlyContain(l =>
            l.ItemId == f.Right.Id && l.PackagingLevelId == f.RightBoxId && l.ConversionFactor == 12m
        );
        f.Line.ItemId.Should().Be(f.Right.Id);
        f.Sibling.ItemId.Should().Be(f.Right.Id);
        f.Other.ItemId.Should().BeNull();
        // Equivalencia futura: una sola relación activa, hacia C (la de B quedó deshabilitada).
        f.CodeOwner.Should().Be(f.Right.Id);
        f.Confirmation.Verify(
            c => c.UnconfirmAsync(f.Document, f.Line, UserId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.Confirmation.Verify(
            c =>
                c.ConfirmAsync(
                    f.Document,
                    It.IsAny<PurchaseReceptionLine>(),
                    f.Right.Id,
                    UserId,
                    It.IsAny<DateTime>(),
                    f.RightBoxId,
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
        f.AutoMatcher.Verify(
            a => a.RefreshAsync(f.Document, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Confirmed_purchase_is_never_modified()
    {
        var f = new Fixture();
        var invoice = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor",
            "1791352688001",
            "01",
            "001-001-000000001",
            new DateOnly(2026, 9, 27),
            UserId,
            Guid.NewGuid(),
            "Contado",
            1,
            30
        );
        typeof(PurchaseInvoice).GetProperty(nameof(PurchaseInvoice.Status))!
            .SetValue(invoice, PurchaseStatus.Confirmed);
        f.Purchases
            .Setup(p => p.GetByAccessKeyAsync(TenantId, f.Document.AccessKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var result = await f.CorrectAsync();

        result.IsSuccess.Should().BeFalse();
        f.Line.ItemId.Should().Be(f.Wrong.Id);
        f.CodeOwner.Should().Be(f.Wrong.Id);
        f.UnitOfWork.Verify(
            u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<CancellationToken, Task>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Code_owned_by_a_third_product_is_not_overwritten()
    {
        var f = new Fixture { CodeOwner = Guid.NewGuid() };

        var result = await f.CorrectAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("otro producto");
        f.Confirmation.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Primary_code_on_previous_product_rolls_back_instead_of_leaving_two_relations()
    {
        var f = new Fixture { CodeIsPrimaryOnOwner = true };

        var result = await f.CorrectAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("principal");
        f.Confirmation.Verify(
            c =>
                c.ConfirmAsync(
                    It.IsAny<PurchaseReceptionDocument>(),
                    It.IsAny<PurchaseReceptionLine>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        f.UnitOfWork.Verify(u => u.ClearChangeTracker(), Times.Once);
    }

    [Fact]
    public async Task Inventory_product_requires_the_supplier_presentation()
    {
        var f = new Fixture();

        var result = await f.Build()
            .Handle(
                new CorrectReceptionLineMatchCommand(f.Line.Id, f.Right.Id, null),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("presentación");
        f.Line.ItemId.Should().Be(f.Wrong.Id);
    }
}
