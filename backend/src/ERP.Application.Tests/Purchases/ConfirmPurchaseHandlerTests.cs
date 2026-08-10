using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ERP.Application.Tests.Purchases;

public sealed class ConfirmPurchaseHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid ItemId1 = Guid.NewGuid();
    private static readonly Guid ItemId2 = Guid.NewGuid();
    private static readonly Guid WhId = Guid.NewGuid();
    private static readonly Guid PtId = Guid.NewGuid();

    private static PurchaseInvoice CreateDraftInvoice(int lineCount = 1, bool sameItem = false)
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PtId,
            "Contado",
            1,
            30,
            globalWarehouseId: WhId
        );

        var lines = new List<PurchaseInvoiceDetail>();
        for (var i = 0; i < lineCount; i++)
        {
            var itemId = sameItem ? ItemId1 : (i == 0 ? ItemId1 : ItemId2);
            lines.Add(
                PurchaseInvoiceDetail.Create(
                    inv.Id,
                    TenantId,
                    $"Producto {i + 1}",
                    quantity: 5,
                    unitPrice: 10.00m,
                    vatCode: "10",
                    uomCode: "UNIT",
                    itemId: itemId,
                    warehouseId: WhId
                )
            );
        }
        inv.ReplaceLines(lines, UserId);
        return inv;
    }

    private (
        ConfirmPurchaseHandler handler,
        Mock<IPurchaseInvoiceRepository> repo,
        Mock<IStockRepository> stockRepo
    ) BuildHandler(PurchaseInvoice inv, bool irbpnrConfigured = false)
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        repo.Setup(r => r.ClearScheduleTrackingAsync(inv.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var stockRepo = new Mock<IStockRepository>();
        stockRepo
            .Setup(s =>
                s.GetStockAsync(TenantId, WhId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CurrentStock?)null);
        stockRepo
            .Setup(s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<Guid>(),
                    WhId,
                    It.IsAny<StockMovementType>(),
                    It.IsAny<decimal>(),
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string?>(),
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid?>()
                )
            )
            // 17 parámetros excede la aridad máxima soportada por los overloads genéricos
            // Callback<T1..T16>/Returns<T1..T16> de Moq (P0-02 Fase 6 agregó sourceDocLineId al
            // final) — se usa la API no genérica basada en IInvocation.Arguments por índice.
            .Returns(
                (IInvocation invocation) =>
                {
                    var args = invocation.Arguments;
                    var tid = (Guid)args[0]!;
                    var cid = (Guid)args[1]!;
                    var pid = (Guid)args[2]!;
                    var wid = (Guid)args[3]!;
                    var mt = (StockMovementType)args[4]!;
                    var qty = (decimal)args[5]!;
                    var uom = (string)args[6]!;
                    var eff = (DateOnly)args[7]!;
                    var reference = (string?)args[8];
                    var srcId = (Guid?)args[9];
                    var srcType = (string?)args[10];
                    var actor = (Guid)args[11]!;
                    var cost = (decimal?)args[12];

                    return Task.FromResult(
                        StockMovement.Create(
                            tid,
                            BranchId,
                            pid,
                            wid,
                            mt,
                            qty,
                            uom,
                            previousQuantity: 0,
                            sequenceNumber: 1,
                            runningAverageCost: cost ?? 0m,
                            runningStockValue: 0m,
                            effectiveDate: eff,
                            reference: reference,
                            sourceDocId: srcId,
                            sourceDocType: srcType,
                            createdBy: actor,
                            companyId: cid,
                            unitCost: cost
                        )
                    );
                }
            );
        stockRepo
            .Setup(s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var itemRepo = new Mock<IItemRepository>();
        itemRepo
            .Setup(r =>
                r.GetByIdLightAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((ERP.Domain.Modules.Items.Entities.Item?)null);

        var tax = new Mock<ISriTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ERP.Application.Common.Services.TaxRateResult(15m, "IVA 15%"));

        var postingEngine = new Mock<IPostingEngine>();
        postingEngine
            .Setup(p =>
                p.IsAmountKindConfiguredAsync(
                    TenantId,
                    CompanyId,
                    "Purchases",
                    "InvoiceReceived",
                    PostingAmountKind.TaxIrbpnr,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(irbpnrConfigured);

        var logger = new Mock<ILogger<ConfirmPurchaseHandler>>();

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);

        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);

        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);
        user.Setup(u => u.Email).Returns("test@test.com");
        user.Setup(u => u.FullName).Returns("Test User");

        var handler = new ConfirmPurchaseHandler(
            repo.Object,
            stockRepo.Object,
            itemRepo.Object,
            tax.Object,
            postingEngine.Object,
            logger.Object,
            tenant.Object,
            company.Object,
            user.Object
        );

        return (handler, repo, stockRepo);
    }

    private static decimal ExpectedGrandTotal(PurchaseInvoice inv)
    {
        // After ApplyTaxes(15%) + DistributeCosts(0,0) + Confirm (freeze)
        // Each line: qty=5, price=10 → subtotal=50, disc=0 → taxable=50
        // VAT 15% on 50 = 7.50 → total per line = 57.50
        return inv.Lines.Sum(l =>
        {
            var sub = l.Quantity * l.UnitPrice;
            var vat = Math.Round(sub * 0.15m, 2);
            return sub + vat;
        });
    }

    [Fact]
    public async Task Confirm_single_line_succeeds_and_creates_stock_movement()
    {
        var inv = CreateDraftInvoice(1);
        var (handler, repo, stockRepo) = BuildHandler(inv);
        var total = ExpectedGrandTotal(inv);

        var schedule = new List<ConfirmScheduleInput>
        {
            new(1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), total, null),
        };
        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id, schedule),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Status.Should().Be("Confirmed");

        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    ItemId1,
                    WhId,
                    StockMovementType.PurchaseEntry,
                    5m,
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    inv.Id,
                    "PurchaseInvoice",
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        stockRepo.Verify(
            s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Confirm_two_lines_same_item_creates_two_stock_movements()
    {
        var inv = CreateDraftInvoice(2, sameItem: true);
        var (handler, _, stockRepo) = BuildHandler(inv);
        var total = ExpectedGrandTotal(inv);

        var schedule = new List<ConfirmScheduleInput>
        {
            new(1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), total, null),
        };
        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id, schedule),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    ItemId1,
                    WhId,
                    StockMovementType.PurchaseEntry,
                    5m,
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    inv.Id,
                    "PurchaseInvoice",
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task Confirm_two_lines_different_items_creates_stock_per_item()
    {
        var inv = CreateDraftInvoice(2, sameItem: false);
        var (handler, _, stockRepo) = BuildHandler(inv);
        var total = ExpectedGrandTotal(inv);

        var schedule = new List<ConfirmScheduleInput>
        {
            new(1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), total, null),
        };
        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id, schedule),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    ItemId1,
                    WhId,
                    StockMovementType.PurchaseEntry,
                    5m,
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    inv.Id,
                    "PurchaseInvoice",
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    ItemId2,
                    WhId,
                    StockMovementType.PurchaseEntry,
                    5m,
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    inv.Id,
                    "PurchaseInvoice",
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Confirm_populates_tax_summaries_from_lines()
    {
        var inv = CreateDraftInvoice(1);
        var (handler, _, _) = BuildHandler(inv);
        var total = ExpectedGrandTotal(inv);

        var schedule = new List<ConfirmScheduleInput>
        {
            new(1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), total, null),
        };
        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id, schedule),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        inv.TaxSummaries.Should().ContainSingle();
        var summary = inv.TaxSummaries.Single();
        summary.VatCode.Should().Be("10");
        summary.VatRate.Should().Be(15m);
        summary.TotalAmount.Should()
            .Be(summary.TaxableBase + summary.IceAmount + summary.VatAmount);
    }

    private static void AttachIrbpnr(PurchaseInvoiceDetail line, decimal amount) =>
        line.ReplaceTaxes(
            [
                ERP.Domain.Modules.Purchases.Entities.PurchaseInvoiceDetailTax.Create(
                    line.Id,
                    TenantId,
                    "5",
                    "5001",
                    "IRBPNR",
                    0.02m,
                    ERP.Domain.Modules.SriCatalogs.Enums.SriTaxCalculationType.Specific,
                    line.TaxableBase,
                    amount,
                    ERP.Domain.Modules.Purchases.Enums.PurchaseTaxSource.Xml
                ),
            ]
        );

    [Fact]
    public async Task Confirm_blocks_a_purchase_with_IRBPNR_when_no_PostingRuleLine_is_configured()
    {
        // FLOW-READY-02F.2 — el bloqueo ya no es incondicional (02F.1): ahora depende de si existe
        // una PostingRuleLine para TaxIrbpnr (aquí el mock de IPostingEngine responde "no
        // configurado", vía irbpnrConfigured: false por defecto en BuildHandler).
        var inv = CreateDraftInvoice(1);
        AttachIrbpnr(inv.Lines[0], 0.48m);
        var (handler, _, stockRepo) = BuildHandler(inv);

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("IRBPNR");
        result.Error.Should().Contain("PostingRuleLine");
        inv.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft);
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<StockMovementType>(),
                    It.IsAny<decimal>(),
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid>(),
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid?>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Confirm_succeeds_with_IRBPNR_when_PostingRuleLine_is_configured()
    {
        // FLOW-READY-02F.2 — con la configuración contable presente (mock devuelve true), la
        // compra confirma y GrandTotal/PurchasePayable incluyen el monto IRBPNR.
        var inv = CreateDraftInvoice(1);
        var line = inv.Lines[0];
        AttachIrbpnr(line, 0.48m);
        var (handler, repo, _) = BuildHandler(inv, irbpnrConfigured: true);

        var expectedGrandTotal = ExpectedGrandTotal(inv) + 0.48m;
        var schedule = new List<ConfirmScheduleInput>
        {
            new(1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), expectedGrandTotal, null),
        };

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id, schedule),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue($"Error: {result.Error}");
        inv.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed);
        result.Value!.GrandTotal.Should().Be(expectedGrandTotal);
        line.IrbpnrAmount.Should().Be(0.48m);
        repo.Verify(
            r => r.TrackPayable(It.Is<PurchasePayable>(p => p.TotalAmount == expectedGrandTotal)),
            Times.Once
        );
    }

    [Fact]
    public async Task Confirm_without_IRBPNR_is_unaffected_by_the_guard()
    {
        // Regresión — el guard nuevo no debe afectar compras sin IRBPNR (mayoría de los casos).
        var inv = CreateDraftInvoice(1);
        var (handler, _, _) = BuildHandler(inv);

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        inv.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed);
    }

    [Fact]
    public async Task Confirm_not_found_returns_failure()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseInvoice?)null);

        var inv = CreateDraftInvoice(1);
        var (handler, _, _) = BuildHandler(inv);

        var fakeId = Guid.NewGuid();
        var repoOverride = new Mock<IPurchaseInvoiceRepository>();
        repoOverride
            .Setup(r => r.GetByIdAsync(TenantId, fakeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseInvoice?)null);

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var h = new ConfirmPurchaseHandler(
            repoOverride.Object,
            new Mock<IStockRepository>().Object,
            new Mock<IItemRepository>().Object,
            new Mock<ISriTaxResolver>().Object,
            new Mock<IPostingEngine>().Object,
            new Mock<ILogger<ConfirmPurchaseHandler>>().Object,
            tenant.Object,
            company.Object,
            user.Object
        );

        var result = await h.Handle(new ConfirmPurchaseCommand(fakeId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no encontrada");
    }

    [Fact]
    public async Task Confirm_clears_schedule_tracking_before_generating()
    {
        var inv = CreateDraftInvoice(1);
        var (handler, repo, _) = BuildHandler(inv);
        var total = ExpectedGrandTotal(inv);

        var schedule = new List<ConfirmScheduleInput>
        {
            new(1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), total, null),
        };
        await handler.Handle(new ConfirmPurchaseCommand(inv.Id, schedule), CancellationToken.None);

        repo.Verify(
            r => r.ClearScheduleTrackingAsync(inv.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Confirm_creates_payable_with_correct_total()
    {
        var inv = CreateDraftInvoice(1);
        var (handler, repo, _) = BuildHandler(inv);
        var total = ExpectedGrandTotal(inv);

        var schedule = new List<ConfirmScheduleInput>
        {
            new(1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), total, null),
        };
        await handler.Handle(new ConfirmPurchaseCommand(inv.Id, schedule), CancellationToken.None);

        repo.Verify(
            r => r.TrackPayable(It.Is<PurchasePayable>(p => p.TotalAmount > 0)),
            Times.Once
        );
    }

    [Fact]
    public async Task Confirm_generates_schedule_automatically_when_no_schedule_sent()
    {
        var inv = CreateDraftInvoice(1);
        var (handler, _, _) = BuildHandler(inv);

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.PaymentSchedules.Should().HaveCount(1);
        result.Value.PaymentSchedules[0].Amount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Full_flow_draft_then_confirm_produces_correct_state()
    {
        // 1. Crear borrador con 2 líneas (1 producto repetido + 1 distinto)
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Corporación Logística S.A.",
            "1790987654001",
            "01",
            "001-500-000000099",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PtId,
            "Crédito 30 días",
            1,
            30,
            globalWarehouseId: WhId,
            taxSupportCode: "01",
            sriPaymentMethodCode: "20"
        );

        var lines = new List<PurchaseInvoiceDetail>
        {
            PurchaseInvoiceDetail.Create(
                inv.Id,
                TenantId,
                "Camisa Oxford Blue",
                quantity: 12,
                unitPrice: 25.50m,
                vatCode: "10",
                uomCode: "UNIT",
                itemId: ItemId1,
                warehouseId: WhId,
                discountPct: 5,
                snapshotSku: "1002",
                snapshotItemName: "Camisa Oxford Blue"
            ),
            PurchaseInvoiceDetail.Create(
                inv.Id,
                TenantId,
                "Pantalón Gabardina Slim",
                quantity: 5,
                unitPrice: 42.00m,
                vatCode: "10",
                uomCode: "UNIT",
                itemId: ItemId2,
                warehouseId: WhId,
                snapshotSku: "5042",
                snapshotItemName: "Pantalón Gabardina Slim"
            ),
            PurchaseInvoiceDetail.Create(
                inv.Id,
                TenantId,
                "Camisa Oxford Blue (dup)",
                quantity: 3,
                unitPrice: 25.50m,
                vatCode: "10",
                uomCode: "UNIT",
                itemId: ItemId1,
                warehouseId: WhId,
                snapshotSku: "1002",
                snapshotItemName: "Camisa Oxford Blue"
            ),
        };
        inv.ReplaceLines(lines, UserId);

        // Verificar estado borrador
        inv.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft);
        inv.Lines.Should().HaveCount(3);

        // 2. Confirmar (sin schedule explícito → auto-genera)
        var (handler, repo, stockRepo) = BuildHandler(inv);

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id),
            CancellationToken.None
        );

        // 3. Verificar resultado
        result.IsSuccess.Should().BeTrue($"el confirm debe ser exitoso. Error: {result.Error}");
        var dto = result.Value!;

        dto.Status.Should().Be("Confirmed");
        dto.Lines.Should().HaveCount(3);
        dto.PaymentSchedules.Should().HaveCount(1);
        dto.PaymentSchedules[0].Amount.Should().BeGreaterThan(0);

        // 4. Verificar stock: 3 movimientos (3 líneas con itemId + warehouseId)
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    It.IsAny<Guid>(),
                    WhId,
                    StockMovementType.PurchaseEntry,
                    It.IsAny<decimal>(),
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    inv.Id,
                    "PurchaseInvoice",
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(3)
        );

        // ItemId1 aparece 2 veces (línea 1 + línea 3)
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    ItemId1,
                    WhId,
                    StockMovementType.PurchaseEntry,
                    It.IsAny<decimal>(),
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    inv.Id,
                    "PurchaseInvoice",
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );

        // ItemId2 aparece 1 vez
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    ItemId2,
                    WhId,
                    StockMovementType.PurchaseEntry,
                    5m,
                    It.IsAny<string>(),
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    inv.Id,
                    "PurchaseInvoice",
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        // 5. Payable creado
        repo.Verify(
            r => r.TrackPayable(It.Is<PurchasePayable>(p => p.TotalAmount > 0)),
            Times.Once
        );

        // 6. Communication creada
        repo.Verify(r => r.TrackCommunication(It.IsAny<PurchaseCommunication>()), Times.Once);

        // 7. Schedule tracking limpiado
        repo.Verify(
            r => r.ClearScheduleTrackingAsync(inv.Id, It.IsAny<CancellationToken>()),
            Times.Once
        );

        // 8. SaveChanges ejecutado (vía IStockRepository, con retry de secuencia del Kardex)
        stockRepo.Verify(
            s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );

        // 9. Todas las líneas congeladas
        inv.Lines.Should().OnlyContain(l => l.IsFrozen);
    }
}
