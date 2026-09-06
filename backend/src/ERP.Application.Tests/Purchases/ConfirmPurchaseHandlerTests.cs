using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Payables.Entities;
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

    /// <summary>ADR-033, Fase 2 P1: condición de pago activa por defecto en estas pruebas (foco en
    /// costeo/stock/posting, no en el guard de IsActive — cubierto en su propio test).</summary>
    private static Mock<IPaymentTermRepository> ActivePaymentTermRepoMock()
    {
        var repo = new Mock<IPaymentTermRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentTerm.Create(TenantId, "CONT", "Contado", 1, 0, UserId));
        return repo;
    }

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

    private static PurchaseInvoice CreateDraftInvoiceWithPackagedLine()
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000002",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PtId,
            "Contado",
            1,
            30,
            globalWarehouseId: WhId
        );

        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Fanta Harmony NRJ 1350 PET(12)",
            quantity: 2m,
            unitPrice: 9.29m,
            vatCode: "10",
            uomCode: "PACA",
            itemId: ItemId1,
            warehouseId: WhId,
            conversionFactor: 12m,
            baseUomCode: "UNIT"
        );
        inv.ReplaceLines([line], UserId);
        return inv;
    }

    private static PurchaseInvoice CreateXmlDraftInvoice(
        Guid itemId,
        Guid? packagingLevelId = null,
        decimal conversionFactor = 1m,
        string uomCode = "UNIT"
    )
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000003",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PtId,
            "Contado",
            1,
            30,
            globalWarehouseId: WhId
        );

        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto XML",
            quantity: 2m,
            unitPrice: 9.29m,
            vatCode: "10",
            uomCode: uomCode,
            itemId: itemId,
            warehouseId: WhId,
            conversionFactor: conversionFactor,
            purchaseReceptionLineId: Guid.NewGuid(),
            baseUomCode: "UNIT",
            packagingLevelId: packagingLevelId
        );
        inv.ReplaceLines([line], UserId);
        return inv;
    }

    private static PurchaseInvoice CreateDraftInvoiceWithCustomLine(
        Guid itemId,
        decimal quantity,
        decimal unitPrice,
        string description = "Producto margen"
    )
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000004",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PtId,
            "Contado",
            1,
            30,
            globalWarehouseId: WhId
        );

        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            description,
            quantity: quantity,
            unitPrice: unitPrice,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: itemId,
            warehouseId: WhId
        );
        inv.ReplaceLines([line], UserId);
        return inv;
    }

    private static Item CreateItem(bool tracksStock = true)
    {
        var item = Item.Create(
            TenantId,
            "SKU-XML",
            "Item XML",
            "Item XML",
            Guid.NewGuid(),
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(tracksStock),
            UserId
        );
        item.ReplacePackagingLevels(
            [
                ("UNIDAD X1", 1, 1m, "UNIT", null, null, true, false, true),
                ("PACA X12", 2, 12m, "PACA", null, null, false, true, false),
            ],
            UserId
        );
        return item;
    }

    private (
        ConfirmPurchaseHandler handler,
        Mock<IPurchaseInvoiceRepository> repo,
        Mock<IStockRepository> stockRepo,
        Mock<IAccountsPayableService> payables
    ) BuildHandler(
        PurchaseInvoice inv,
        bool irbpnrConfigured = false,
        Item? itemForXmlLines = null,
        Item? itemForMarginGuard = null,
        decimal? marginGuardSalePrice = null,
        bool allowConfirmWithoutReceptionXml = true,
        Guid? activeBranchId = null
    )
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

        var warehouse = Warehouse.Create(
            TenantId,
            BranchId,
            "Bodega Principal",
            "WH-01",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            UserId,
            CompanyId
        );
        var whRepo = new Mock<IWarehouseRepository>();
        whRepo
            .Setup(r => r.GetByIdAsync(TenantId, WhId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouse);

        var itemRepo = new Mock<IItemRepository>();
        if (itemForXmlLines is not null)
        {
            itemRepo
                .Setup(r =>
                    r.GetByIdAsync(
                        itemForXmlLines.Id,
                        TenantId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(itemForXmlLines);
        }
        itemRepo
            .Setup(r =>
                r.GetByIdLightAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((ERP.Domain.Modules.Items.Entities.Item?)null);
        if (itemForMarginGuard is not null)
        {
            itemRepo
                .Setup(r =>
                    r.GetByIdLightAsync(
                        itemForMarginGuard.Id,
                        TenantId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(itemForMarginGuard);
        }

        var pricingResolver = new Mock<IPricingResolver>();
        if (itemForMarginGuard is not null && marginGuardSalePrice is not null)
        {
            pricingResolver
                .Setup(p =>
                    p.ResolveAsync(itemForMarginGuard.Id, null, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync(
                    Result<PricingResult>.Success(
                        new PricingResult(
                            itemForMarginGuard.Id,
                            Guid.NewGuid(),
                            "GEN",
                            "Lista General",
                            "USD",
                            marginGuardSalePrice.Value,
                            null,
                            marginGuardSalePrice.Value
                        )
                    )
                );
        }
        else
        {
            pricingResolver
                .Setup(p =>
                    p.ResolveAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Result<PricingResult>.Failure("Sin precio configurado."));
        }

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

        var branch = new Mock<ICurrentBranch>();
        branch.Setup(b => b.BranchId).Returns(activeBranchId ?? BranchId);

        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);
        user.Setup(u => u.Email).Returns("test@test.com");
        user.Setup(u => u.FullName).Returns("Test User");

        var preferences = new Mock<IOperationalPreferencesResolver>();
        preferences
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultOperationalPreferences(allowConfirmWithoutReceptionXml));

        var payables = new Mock<IAccountsPayableService>();
        payables
            .Setup(p =>
                p.StageFromOriginAsync(
                    It.IsAny<CreateAccountsPayableFromOriginRequest>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (CreateAccountsPayableFromOriginRequest req, Guid createdBy, CancellationToken _) =>
                {
                    var payable = AccountsPayable.CreateFromOrigin(
                        req.TenantId, req.CompanyId, req.BranchId, req.SupplierId,
                        req.OriginType, req.OriginId, req.DocumentType, req.DocumentNumber,
                        req.IssueDate, req.AccountingDate, createdBy
                    );
                    foreach (var installment in req.Installments)
                        payable.AddInstallment(installment.InstallmentNumber, installment.DueDate, installment.Amount);
                    return payable;
                }
            );

        var handler = new ConfirmPurchaseHandler(
            repo.Object,
            stockRepo.Object,
            itemRepo.Object,
            whRepo.Object,
            ActivePaymentTermRepoMock().Object,
            tax.Object,
            postingEngine.Object,
            pricingResolver.Object,
            payables.Object,
            logger.Object,
            tenant.Object,
            company.Object,
            branch.Object,
            user.Object,
            preferences.Object
        );

        return (handler, repo, stockRepo, payables);
    }

    /// <summary>Defaults que preservan el comportamiento vigente antes de CONFIG-DYNAMIC-OPERATIONS-02 (AllowConfirmWithoutReceptionXml=true → sin bloqueo, igual que siempre).</summary>
    internal static OperationalPreferences DefaultOperationalPreferences(
        bool allowConfirmWithoutReceptionXml = true
    ) =>
        new(
            SalesPos: new SalesPosPreferences(true, false, true, 0m, null, false, false, null, null),
            Cash: new CashPreferences(true, true, 0m, true, true, true),
            Purchases: new PurchasesPreferences(
                null,
                allowConfirmWithoutReceptionXml,
                true,
                true,
                false
            ),
            Inventory: new InventoryPreferences(false, true, false, 0m),
            Printing: new PrintingPreferences("AskBeforePrint", 1, "80mm", false, true, true, false),
            ElectronicDocuments: new ElectronicDocumentsPreferences(true, 3, true, true),
            Notifications: new NotificationsPreferences(true, false, "es")
        );

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
        var (handler, repo, stockRepo, payables) = BuildHandler(inv);
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

    /// <summary>
    /// CONFIG-DYNAMIC-OPERATIONS-02 (purchases.allow_confirm_without_reception_xml=false): factura
    /// sin ninguna línea vinculada a una recepción XML/TXT (PurchaseReceptionLineId null en todas
    /// las líneas, como CreateDraftInvoice) debe rechazarse al confirmar.
    /// </summary>
    [Fact]
    public async Task Confirm_rechaza_sin_recepcion_xml_si_la_preferencia_no_lo_permite()
    {
        var inv = CreateDraftInvoice(1);
        var (handler, repo, stockRepo, payables) = BuildHandler(
            inv,
            allowConfirmWithoutReceptionXml: false
        );
        var total = ExpectedGrandTotal(inv);

        var schedule = new List<ConfirmScheduleInput>
        {
            new(1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), total, null),
        };
        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id, schedule),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        stockRepo.Verify(
            s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Contraparte de <see cref="Confirm_rechaza_sin_recepcion_xml_si_la_preferencia_no_lo_permite"/>:
    /// misma factura sin recepción XML, pero con la línea vinculada (CreateXmlDraftInvoice ya fija
    /// PurchaseReceptionLineId), debe confirmar sin importar la preferencia.
    /// </summary>
    [Fact]
    public async Task Confirm_permite_sin_recepcion_xml_si_alguna_linea_ya_esta_vinculada()
    {
        var item = CreateItem(tracksStock: true);
        var pacaId = item.PackagingLevels.Single(p => p.UomCode == "PACA").Id;
        var inv = CreateXmlDraftInvoice(
            item.Id,
            packagingLevelId: pacaId,
            conversionFactor: 12m,
            uomCode: "PACA"
        );
        var (handler, _, stockRepo, _) = BuildHandler(
            inv,
            itemForXmlLines: item,
            allowConfirmWithoutReceptionXml: false
        );

        var result = await handler.Handle(new ConfirmPurchaseCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        stockRepo.Verify(
            s => s.SaveChangesWithSequenceRetryAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Confirm_con_presentacion_de_compra_mueve_inventario_en_unidad_base()
    {
        var inv = CreateDraftInvoiceWithPackagedLine();
        var (handler, _, stockRepo, _) = BuildHandler(inv);

        var result = await handler.Handle(new ConfirmPurchaseCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue($"Error: {result.Error}");
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    ItemId1,
                    WhId,
                    StockMovementType.PurchaseEntry,
                    24m,
                    "UNIT",
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    inv.Id,
                    "PurchaseInvoice",
                    UserId,
                    0.774167m,
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Confirm_bloquea_compra_xml_de_item_inventariable_sin_presentacion()
    {
        var item = CreateItem(tracksStock: true);
        var inv = CreateXmlDraftInvoice(item.Id);
        var (handler, _, stockRepo, _) = BuildHandler(inv, itemForXmlLines: item);

        var result = await handler.Handle(new ConfirmPurchaseCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ítem inventariable sin presentación");
        stockRepo.Verify(
            s => s.AppendMovementAsync(
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
    public async Task Confirm_permite_compra_xml_de_PACA_X12_con_presentacion_vinculada()
    {
        var item = CreateItem(tracksStock: true);
        var pacaId = item.PackagingLevels.Single(p => p.UomCode == "PACA").Id;
        var inv = CreateXmlDraftInvoice(
            item.Id,
            packagingLevelId: pacaId,
            conversionFactor: 12m,
            uomCode: "PACA"
        );
        var (handler, _, stockRepo, _) = BuildHandler(inv, itemForXmlLines: item);

        var result = await handler.Handle(new ConfirmPurchaseCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    item.Id,
                    WhId,
                    StockMovementType.PurchaseEntry,
                    24m,
                    "UNIT",
                    It.IsAny<DateOnly>(),
                    It.IsAny<string?>(),
                    inv.Id,
                    "PurchaseInvoice",
                    UserId,
                    It.IsAny<decimal?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid?>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Confirm_two_lines_same_item_creates_two_stock_movements()
    {
        var inv = CreateDraftInvoice(2, sameItem: true);
        var (handler, _, stockRepo, _) = BuildHandler(inv);
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
        var (handler, _, stockRepo, _) = BuildHandler(inv);
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
        var (handler, _, _, _) = BuildHandler(inv);
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
        var (handler, _, stockRepo, _) = BuildHandler(inv);

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
        var (handler, _, _, payables) = BuildHandler(inv, irbpnrConfigured: true);

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
        payables.Verify(
            p =>
                p.StageFromOriginAsync(
                    It.Is<CreateAccountsPayableFromOriginRequest>(req =>
                        req.Installments.Sum(i => i.Amount) == expectedGrandTotal
                    ),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Confirm_without_IRBPNR_is_unaffected_by_the_guard()
    {
        // Regresión — el guard nuevo no debe afectar compras sin IRBPNR (mayoría de los casos).
        var inv = CreateDraftInvoice(1);
        var (handler, _, _, _) = BuildHandler(inv);

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        inv.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed);
    }

    [Fact]
    public async Task Confirm_bloqueado_si_condicion_de_pago_fue_desactivada_despues_del_borrador()
    {
        // ADR-033, Fase 2 P1: el snapshot del borrador no refleja que la condición de pago fue
        // desactivada después de crearlo — la confirmación debe bloquear con mensaje claro.
        var inv = CreateDraftInvoice(1);

        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);

        var inactiveTerm = PaymentTerm.Create(TenantId, "30D", "30 días", 1, 30, UserId);
        inactiveTerm.Disable(UserId);
        var ptRepo = new Mock<IPaymentTermRepository>();
        ptRepo
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveTerm);

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var branch = new Mock<ICurrentBranch>();
        branch.Setup(b => b.BranchId).Returns(BranchId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var handler = new ConfirmPurchaseHandler(
            repo.Object,
            Mock.Of<IStockRepository>(),
            Mock.Of<IItemRepository>(),
            Mock.Of<IWarehouseRepository>(),
            ptRepo.Object,
            Mock.Of<ISriTaxResolver>(),
            Mock.Of<IPostingEngine>(),
            Mock.Of<IPricingResolver>(),
            Mock.Of<IAccountsPayableService>(),
            Mock.Of<ILogger<ConfirmPurchaseHandler>>(),
            tenant.Object,
            company.Object,
            branch.Object,
            user.Object,
            Mock.Of<IOperationalPreferencesResolver>()
        );

        var result = await handler.Handle(new ConfirmPurchaseCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        inv.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Draft);
    }

    [Fact]
    public async Task Confirm_not_found_returns_failure()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseInvoice?)null);

        var inv = CreateDraftInvoice(1);
        var (handler, _, _, _) = BuildHandler(inv);

        var fakeId = Guid.NewGuid();
        var repoOverride = new Mock<IPurchaseInvoiceRepository>();
        repoOverride
            .Setup(r => r.GetByIdAsync(TenantId, fakeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseInvoice?)null);

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.CompanyId).Returns(CompanyId);
        var branch = new Mock<ICurrentBranch>();
        branch.Setup(b => b.BranchId).Returns(BranchId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var preferencesOverride = new Mock<IOperationalPreferencesResolver>();
        preferencesOverride
            .Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultOperationalPreferences());

        var h = new ConfirmPurchaseHandler(
            repoOverride.Object,
            new Mock<IStockRepository>().Object,
            new Mock<IItemRepository>().Object,
            new Mock<IWarehouseRepository>().Object,
            ActivePaymentTermRepoMock().Object,
            new Mock<ISriTaxResolver>().Object,
            new Mock<IPostingEngine>().Object,
            new Mock<IPricingResolver>().Object,
            new Mock<IAccountsPayableService>().Object,
            new Mock<ILogger<ConfirmPurchaseHandler>>().Object,
            tenant.Object,
            company.Object,
            branch.Object,
            user.Object,
            preferencesOverride.Object
        );

        var result = await h.Handle(new ConfirmPurchaseCommand(fakeId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no encontrada");
    }

    [Fact]
    public async Task Confirm_clears_schedule_tracking_before_generating()
    {
        var inv = CreateDraftInvoice(1);
        var (handler, repo, _, _) = BuildHandler(inv);
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
        var (handler, _, _, payables) = BuildHandler(inv);
        var total = ExpectedGrandTotal(inv);

        var schedule = new List<ConfirmScheduleInput>
        {
            new(1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), total, null),
        };
        await handler.Handle(new ConfirmPurchaseCommand(inv.Id, schedule), CancellationToken.None);

        payables.Verify(
            p =>
                p.StageFromOriginAsync(
                    It.Is<CreateAccountsPayableFromOriginRequest>(req =>
                        req.Installments.Sum(i => i.Amount) > 0
                    ),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Confirm_generates_schedule_automatically_when_no_schedule_sent()
    {
        var inv = CreateDraftInvoice(1);
        var (handler, _, _, _) = BuildHandler(inv);

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
        var (handler, repo, stockRepo, payables) = BuildHandler(inv);

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
        payables.Verify(
            p =>
                p.StageFromOriginAsync(
                    It.Is<CreateAccountsPayableFromOriginRequest>(req =>
                        req.Installments.Sum(i => i.Amount) > 0
                    ),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
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

    // ── PURCHASE-BACKEND-SUSPICIOUS-PACKAGING-COST-GUARD-01 ─────────────────

    [Fact]
    public async Task Confirm_bloquea_presentacion_con_costo_sospechoso()
    {
        // Caso real: CLUB PLATINO LATA 355CC NRB X6 TERMO cargado como UNIDAD X1
        // en vez de SIXPACK X6 -> LandedUnitCost muy por encima del PVP (margen ≈ -373.05%).
        var item = CreateItem(tracksStock: true);
        var inv = CreateDraftInvoiceWithCustomLine(item.Id, quantity: 1m, unitPrice: 5.1090m);
        var (handler, _, stockRepo, _) = BuildHandler(
            inv,
            itemForMarginGuard: item,
            marginGuardSalePrice: 1.08m
        );

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("presentación/costo sospechoso");
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
    public async Task Confirm_permite_presentacion_con_costo_correcto()
    {
        // Mismo producto cargado como SIXPACK X6 -> margen ≈ 21.16%, no bloquea.
        var item = CreateItem(tracksStock: true);
        var inv = CreateDraftInvoiceWithCustomLine(item.Id, quantity: 1m, unitPrice: 0.8515m);
        var (handler, _, stockRepo, _) = BuildHandler(
            inv,
            itemForMarginGuard: item,
            marginGuardSalePrice: 1.08m
        );

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue($"Error: {result.Error}");
        inv.Status.Should().Be(ERP.Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed);
        stockRepo.Verify(
            s =>
                s.AppendMovementAsync(
                    TenantId,
                    CompanyId,
                    item.Id,
                    WhId,
                    StockMovementType.PurchaseEntry,
                    1m,
                    "UNIT",
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
    public async Task Confirm_no_bloquea_por_margen_sin_precio_de_venta_conocido()
    {
        // Sin precio de venta resuelto (pricing resolver falla / sin PricingRule/BaseSalePrice)
        // el guard no puede evaluar margen -> no bloquea.
        var item = CreateItem(tracksStock: true);
        var inv = CreateDraftInvoiceWithCustomLine(item.Id, quantity: 1m, unitPrice: 5.1090m);
        var (handler, _, _, _) = BuildHandler(inv, itemForMarginGuard: item, marginGuardSalePrice: null);

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue($"Error: {result.Error}");
    }

    [Fact]
    public async Task Confirm_no_bloquea_item_no_inventariable_aunque_margen_sea_extremo()
    {
        var item = CreateItem(tracksStock: false);
        var inv = CreateDraftInvoiceWithCustomLine(item.Id, quantity: 1m, unitPrice: 5.1090m);
        var (handler, _, _, _) = BuildHandler(
            inv,
            itemForMarginGuard: item,
            marginGuardSalePrice: 1.08m
        );

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue($"Error: {result.Error}");
    }

    [Fact]
    public async Task Confirm_no_bloquea_margen_negativo_leve()
    {
        // marginPct = -10% (costo 1.10 vs venta 1.00) — leve, no cruza el umbral de -50%.
        var item = CreateItem(tracksStock: true);
        var inv = CreateDraftInvoiceWithCustomLine(item.Id, quantity: 1m, unitPrice: 1.10m);
        var (handler, _, _, _) = BuildHandler(
            inv,
            itemForMarginGuard: item,
            marginGuardSalePrice: 1.00m
        );

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue($"Error: {result.Error}");
    }

    [Fact]
    public async Task Confirm_no_bloquea_linea_sin_itemId()
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000005",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PtId,
            "Contado",
            1,
            30,
            globalWarehouseId: WhId
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Servicio de transporte",
            quantity: 1m,
            unitPrice: 100m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: null,
            warehouseId: null
        );
        inv.ReplaceLines([line], UserId);
        var (handler, _, _, _) = BuildHandler(inv);

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue($"Error: {result.Error}");
    }

    /// <summary>
    /// Hallazgo ALTO auditoría de aislamiento (Sales/Purchases cross-branch): ConfirmPurchaseCommand
    /// está marcado IBranchScopedRequest, pero ese marker solo exige sucursal activa autorizada — no
    /// garantiza que la compra cargada pertenezca a esa sucursal. El guard bodega↔sucursal (STEP 0)
    /// no cubre este caso porque nunca se llega a evaluar la bodega de una compra ajena. Debe
    /// rechazar con NotFound (nunca revelar existencia cross-branch) cuando la compra pertenece a
    /// otra sucursal.
    /// </summary>
    [Fact]
    public async Task Compra_de_otra_sucursal_retorna_NotFound_y_no_la_confirma()
    {
        var inv = CreateDraftInvoice();
        var (handler, _, _, _) = BuildHandler(inv, activeBranchId: Guid.NewGuid());

        var result = await handler.Handle(new ConfirmPurchaseCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ERP.Application.Common.ApiResponseCodes.Common.NotFound);
        inv.Status.Should().Be(Domain.Modules.Purchases.Enums.PurchaseStatus.Draft);
    }

    /// <summary>Misma compra, sucursal activa correcta (BranchId por defecto) — debe seguir confirmándose.</summary>
    [Fact]
    public async Task Compra_de_la_misma_sucursal_sigue_confirmandose_correctamente()
    {
        var inv = CreateDraftInvoice();
        var (handler, _, _, _) = BuildHandler(inv);

        var result = await handler.Handle(new ConfirmPurchaseCommand(inv.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        inv.Status.Should().Be(Domain.Modules.Purchases.Enums.PurchaseStatus.Confirmed);
    }
}
