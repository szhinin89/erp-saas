using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.Companies.UseCases.PrecisionPolicy;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Application.Modules.Inventory.Stock.UseCases.PostInitialBalance;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.InitialLoad;

/// <summary>IL-4A — Inventario Inicial: Validate fiel, saldo al corte, sin historia previa.</summary>
public sealed class InitialStockImportProcessorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly DateOnly CompanyToday = new(2026, 10, 9);
    private const int QuantityDecimals = 3;
    private const int UnitCostDecimals = 4;

    private readonly Mock<IInitialStockImportSheetReader> _reader = new();
    private readonly Mock<IItemRepository> _itemRepo = new();
    private readonly Mock<IWarehouseRepository> _warehouseRepo = new();
    private readonly Mock<IInventoryAdjustmentReasonRepository> _reasonRepo = new();
    private readonly Mock<IInitialStockLookup> _stockLookup = new();
    private readonly Mock<ICompanyPrecisionPolicyProvider> _precision = new();
    private readonly Mock<ICompanyClock> _clock = new();
    private readonly Mock<ICurrentBranch> _branch = new();
    private readonly Mock<IOperationalContext> _ctx = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IOpeningBalanceConstraintsReader> _openingBalance = new();
    private DateOnly? _openingBalanceDate = new(2026, 9, 30);
    private readonly Item _item = BuildItem("PROD-0001");
    private readonly Warehouse _warehouse = BuildWarehouse("BOD-01");

    public InitialStockImportProcessorTests()
    {
        _ctx.SetupGet(x => x.TenantId).Returns(TenantId);
        _ctx.SetupGet(x => x.CompanyId).Returns(CompanyId);
        _branch.SetupGet(x => x.BranchId).Returns(BranchId);
        _clock.Setup(x => x.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(CompanyToday);
        _openingBalance.Setup(x => x.GetOpeningBalanceDateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _openingBalanceDate);
        _precision.Setup(x => x.GetEffectiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new EffectivePrecisionPolicyDto("Standard", 2, 4, QuantityDecimals, 2, UnitCostDecimals, 6, 6, 0.01m,
                false, null, null, 2, 2, 2, 2, 2, 2, 2));
        SetupItem("PROD-0001", _item);
        _warehouseRepo.Setup(x => x.GetAsync(TenantId, null, "BOD-01", BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_warehouse]);
    }

    private InitialStockImportProcessor Processor() => new(_reader.Object, _itemRepo.Object, _warehouseRepo.Object,
        _reasonRepo.Object, _stockLookup.Object, _precision.Object, _clock.Object, _branch.Object, _ctx.Object,
        _mediator.Object, _openingBalance.Object);

    private void SetupItem(string code, Item? item) =>
        _itemRepo.Setup(x => x.ResolveByAnyCodeAsync(code, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(item);

    private static Item BuildItem(string sku, bool availableOnPos = true, bool tracksLot = false,
        bool tracksSeries = false, bool allowDecimalQty = false, ItemNature nature = ItemNature.Product) =>
        Item.Create(TenantId, sku, "Producto " + sku, "Descripción " + sku, Guid.NewGuid(), "19",
            ItemTaxConfig.Create(null, null, null),
            ItemSaleConfig.Create(true, null, false, availableOnPos, false, false, false),
            ItemStockConfig.Create(true, tracksLot, tracksSeries, allowDecimalQty, false, null, null),
            Guid.NewGuid(), companyId: CompanyId, nature: nature);

    private static Warehouse BuildWarehouse(string code) =>
        Warehouse.Create(TenantId, BranchId, "Bodega Principal", code, null, null, null, null, null, null, null,
            null, null, Guid.NewGuid(), CompanyId);

    private static Dictionary<string, string?> Row(string? sku = "PROD-0001", string? quantity = "100",
        string? unitCost = "3.50", string? cutoff = "2026-09-30") => new()
    {
        [InitialStockImportColumns.Sku] = sku,
        [InitialStockImportColumns.Barcode] = null,
        [InitialStockImportColumns.WarehouseCode] = "BOD-01",
        [InitialStockImportColumns.Quantity] = quantity,
        [InitialStockImportColumns.UnitCost] = unitCost,
        [InitialStockImportColumns.CutoffDate] = cutoff,
        [InitialStockImportColumns.Observation] = null,
    };

    private async Task<RowValidationResult> Validate(Dictionary<string, string?> row) =>
        await Processor().ValidateRowAsync(1, row, false, CancellationToken.None);

    private static ParsedInitialStockRow Parsed(RowValidationResult r) =>
        JsonSerializer.Deserialize<ParsedInitialStockRow>(r.ParsedDataJson)!;

    [Fact]
    public async Task Fila_valida_no_genera_issues_y_conserva_la_fecha_de_corte()
    {
        var result = await Validate(Row());

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().BeEmpty();
        var parsed = Parsed(result);
        parsed.ItemId.Should().Be(_item.Id);
        parsed.WarehouseId.Should().Be(_warehouse.Id);
        parsed.WarehouseCode.Should().Be("BOD-01");
        parsed.Quantity.Should().Be(100m);
        parsed.UnitCost.Should().Be(3.50m);
        parsed.CutoffDate.Should().Be(new DateOnly(2026, 9, 30));
    }

    [Theory]
    [InlineData("3.50", 3.50)]
    [InlineData("0.0001", 0.0001)]
    [InlineData("12", 12)]
    public async Task Decimal_con_punto_es_invariante_sin_depender_de_la_cultura(string raw, double expected)
    {
        var result = await Validate(Row(unitCost: raw));

        result.HasBlockingIssue.Should().BeFalse();
        Parsed(result).UnitCost.Should().Be((decimal)expected, "es-EC leería '3.50' como 350");
    }

    [Theory]
    [InlineData("3,50")]
    [InlineData("1,000.50")]
    [InlineData("-5")]
    [InlineData("abc")]
    public async Task Coma_separador_de_miles_signo_o_texto_son_error(string raw)
    {
        var result = await Validate(Row(unitCost: raw));

        result.Issues.Should().ContainSingle(i => i.Code == "INVALID_NUMBER" && i.FieldName == InitialStockImportColumns.UnitCost);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.000")]
    public async Task Cantidad_cero_es_error(string raw)
    {
        var result = await Validate(Row(quantity: raw));

        result.Issues.Should().ContainSingle(i => i.Code == "NON_POSITIVE_NUMBER");
    }

    [Fact]
    public async Task Exceso_de_precision_es_error_sin_redondeo()
    {
        var cost = await Validate(Row(unitCost: "3.12345"));
        cost.Issues.Should().ContainSingle(i => i.Code == "PRECISION_EXCEEDED" && i.FieldName == InitialStockImportColumns.UnitCost)
            .Which.Message.Should().Contain("No se redondea");

        var quantity = await Validate(Row(quantity: "1.2345"));
        quantity.Issues.Should().Contain(i => i.Code == "PRECISION_EXCEEDED" && i.FieldName == InitialStockImportColumns.Quantity);
    }

    [Fact]
    public async Task Cantidad_decimal_en_item_que_no_la_admite_es_error()
    {
        var result = await Validate(Row(quantity: "1.5"));

        result.Issues.Should().ContainSingle(i => i.Code == "DECIMAL_QUANTITY_NOT_ALLOWED");

        SetupItem("DEC-1", BuildItem("DEC-1", allowDecimalQty: true));
        (await Validate(Row(sku: "DEC-1", quantity: "1.5"))).HasBlockingIssue.Should().BeFalse();
    }

    [Fact]
    public async Task Costo_unitario_faltante_es_error()
    {
        var result = await Validate(Row(unitCost: null));

        result.Issues.Should().ContainSingle(i => i.Code == "MISSING_REQUIRED_FIELD" && i.FieldName == InitialStockImportColumns.UnitCost);
    }

    [Fact]
    public async Task Sku_inexistente_es_error()
    {
        SetupItem("NOEXISTE", null);

        var result = await Validate(Row(sku: "NOEXISTE"));

        result.Issues.Should().ContainSingle(i => i.Code == "ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task Sku_y_barcode_de_items_distintos_es_error()
    {
        SetupItem("7861234567890", BuildItem("OTRO"));
        var row = Row();
        row[InitialStockImportColumns.Barcode] = "7861234567890";

        var result = await Validate(row);

        result.Issues.Should().ContainSingle(i => i.Code == "ITEM_CODE_MISMATCH");
    }

    [Fact]
    public async Task Sku_y_barcode_del_mismo_item_es_valido()
    {
        SetupItem("7861234567890", _item);
        var row = Row();
        row[InitialStockImportColumns.Barcode] = "7861234567890";

        (await Validate(row)).HasBlockingIssue.Should().BeFalse();
    }

    [Theory]
    [InlineData("LOT", "ITEM_TRACKS_LOT_OR_SERIES")]
    [InlineData("SER", "ITEM_TRACKS_LOT_OR_SERIES")]
    [InlineData("SRV", "ITEM_NOT_INVENTORIABLE")]
    public async Task Items_con_lote_serie_o_servicio_se_bloquean(string sku, string expected)
    {
        SetupItem(sku, sku switch
        {
            "LOT" => BuildItem(sku, tracksLot: true),
            "SER" => BuildItem(sku, tracksSeries: true),
            _ => BuildItem(sku, nature: ItemNature.Service),
        });

        var result = await Validate(Row(sku: sku));

        result.Issues.Should().ContainSingle(i => i.Code == expected);
    }

    [Fact]
    public async Task Bodega_fuera_de_la_sucursal_activa_es_error_con_indicacion()
    {
        _warehouseRepo.Setup(x => x.GetAsync(TenantId, null, "BOD-99", BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var row = Row();
        row[InitialStockImportColumns.WarehouseCode] = "BOD-99";

        var result = await Validate(row);

        result.Issues.Should().ContainSingle(i => i.Code == "WAREHOUSE_NOT_FOUND")
            .Which.Message.Should().Contain("cambie de sucursal");
    }

    [Fact]
    public async Task Sin_sucursal_activa_es_error()
    {
        _branch.SetupGet(x => x.BranchId).Returns(Guid.Empty);

        var result = await Validate(Row());

        result.Issues.Should().ContainSingle(i => i.Code == "BRANCH_REQUIRED");
    }

    [Fact]
    public async Task Item_bodega_con_historia_previa_es_error()
    {
        _stockLookup.Setup(x => x.HasStockHistoryAsync(_item.Id, _warehouse.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Validate(Row());

        result.Issues.Should().ContainSingle(i => i.Code == "STOCK_HISTORY_EXISTS")
            .Which.Message.Should().Contain("ajuste de inventario normal");
    }

    [Theory]
    [InlineData(null, "MISSING_REQUIRED_FIELD")]
    [InlineData("30-09-2026", "INVALID_CUTOFF_DATE")]
    [InlineData("2026-10-10", "FUTURE_CUTOFF_DATE")]
    public async Task Fecha_de_corte_obligatoria_valida_y_no_futura(string? raw, string expected)
    {
        var result = await Validate(Row(cutoff: raw));

        result.Issues.Should().ContainSingle(i => i.Code == expected && i.FieldName == InitialStockImportColumns.CutoffDate);
    }

    [Fact]
    public async Task Fecha_de_corte_de_hoy_en_la_empresa_es_valida()
    {
        _openingBalanceDate = CompanyToday;
        (await Validate(Row(cutoff: "2026-10-09"))).HasBlockingIssue.Should().BeFalse();
    }

    // ── IL-8E: el corte del inventario inicial es Company.OpeningBalanceDate (igual que CxC/CxP) ──

    [Fact]
    public async Task Validar_corte_distinto_a_la_fecha_de_apertura_bloquea()
    {
        var result = await Validate(Row(cutoff: "2026-09-29"));

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "CUTOFF_DATE_MISMATCH"
            && i.FieldName == InitialStockImportColumns.CutoffDate);
    }

    [Fact]
    public async Task Validar_sin_fecha_de_apertura_definida_bloquea()
    {
        _openingBalanceDate = null;

        var result = await Validate(Row());

        result.Issues.Should().ContainSingle(i => i.Code == "OPENING_BALANCE_DATE_NOT_SET");
    }

    [Theory]
    [InlineData("2026-09-29")]
    [InlineData(null)]
    public async Task Confirmar_con_corte_que_ya_no_es_la_fecha_de_apertura_no_registra_nada(string? opening)
    {
        _openingBalanceDate = opening is null ? null : DateOnly.Parse(opening, System.Globalization.CultureInfo.InvariantCulture);
        SetupConfirmable((_item, _warehouse));

        var result = await Processor().ConfirmBatchAsync([(1, Json(_item, _warehouse))], CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("fecha de apertura");
        _mediator.Verify(m => m.Send(It.IsAny<PostInitialBalanceCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Item_no_disponible_en_pos_genera_warning_no_bloqueante()
    {
        SetupItem("NOPOS", BuildItem("NOPOS", availableOnPos: false));

        var result = await Validate(Row(sku: "NOPOS"));

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "ITEM_NOT_AVAILABLE_ON_POS" && i.Severity == ImportSeverity.Warning);
    }

    [Fact]
    public async Task Duplicado_item_bodega_y_varias_fechas_de_corte_bloquean_el_lote()
    {
        SetupItem("PROD-0002", BuildItem("PROD-0002"));
        var processor = Processor();
        var rows = new List<RowValidationResult>
        {
            await processor.ValidateRowAsync(1, Row(), false, CancellationToken.None),
            await processor.ValidateRowAsync(2, Row(), false, CancellationToken.None),
            await processor.ValidateRowAsync(3, Row(sku: "PROD-0002", cutoff: "2026-09-29"), false, CancellationToken.None),
        };

        var result = processor.ValidateBatch(rows);

        result[0].Issues.Should().Contain(i => i.Code == "DUPLICATE_ITEM_WAREHOUSE_IN_FILE");
        result[1].Issues.Should().Contain(i => i.Code == "DUPLICATE_ITEM_WAREHOUSE_IN_FILE");
        result[2].Issues.Should().NotContain(i => i.Code == "DUPLICATE_ITEM_WAREHOUSE_IN_FILE");
        result.Should().OnlyContain(r => r.Issues.Any(i => i.Code == "MULTIPLE_CUTOFF_DATES") && r.HasBlockingIssue);
    }

    // ── IL-4B: confirmación del lote completo ────────────────────────────────────────────────

    private static readonly DateOnly Cutoff = new(2026, 9, 30);

    private string Json(Item item, Warehouse warehouse, decimal quantity = 10m) =>
        JsonSerializer.Serialize(new ParsedInitialStockRow(item.Id, item.Code.ShortName, "19", warehouse.Id,
            warehouse.Code!, warehouse.Name, quantity, 2.5m, Cutoff, null));

    private void SetupConfirmable(params (Item Item, Warehouse Warehouse)[] pairs)
    {
        _reasonRepo.Setup(x => x.GetByCodeAsync(TenantId, "CARGA_INICIAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(InventoryAdjustmentReason.Create(TenantId, null, "CARGA_INICIAL", "Carga Inicial",
                InventoryAdjustmentReason.Ingreso, false, 0, Guid.NewGuid()));
        foreach (var (item, warehouse) in pairs)
        {
            _itemRepo.Setup(x => x.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(item);
            _warehouseRepo.Setup(x => x.GetByIdAsync(TenantId, warehouse.Id, It.IsAny<CancellationToken>())).ReturnsAsync(warehouse);
        }
        _mediator.Setup(m => m.Send(It.IsAny<PostInitialBalanceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostInitialBalanceCommand c, CancellationToken _) => ERP.Application.Common.Result<StockAdjustmentDto>.Success(
                new StockAdjustmentDto(Guid.NewGuid(), "ADJ-0001", c.WarehouseId, c.WarehouseName, "Ingreso", c.ReasonId,
                    "Carga Inicial", c.Notes, c.CutoffDate, "Executed", DateTime.UtcNow, null, null, null, null, [])));
    }

    [Fact]
    public async Task Confirmar_lote_crea_un_documento_por_bodega_con_la_fecha_de_corte()
    {
        var other = BuildWarehouse("BOD-02");
        var second = BuildItem("PROD-0002");
        SetupConfirmable((_item, _warehouse), (second, _warehouse), (_item, other));

        var result = await Processor().ConfirmBatchAsync(
            [(1, Json(_item, _warehouse)), (2, Json(second, _warehouse)), (3, Json(_item, other))], CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        _mediator.Verify(m => m.Send(It.Is<PostInitialBalanceCommand>(c => c.WarehouseId == _warehouse.Id
            && c.Lines.Count == 2 && c.CutoffDate == Cutoff), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.Is<PostInitialBalanceCommand>(c => c.WarehouseId == other.Id
            && c.Lines.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        result.CreatedIdsByRow![1].Should().Be(result.CreatedIdsByRow[2], "misma bodega → mismo documento");
        result.CreatedIdsByRow[3].Should().NotBe(result.CreatedIdsByRow[1]);
    }

    [Fact]
    public async Task Historia_aparecida_despues_del_preview_aborta_sin_escribir()
    {
        SetupConfirmable((_item, _warehouse));
        _stockLookup.Setup(x => x.HasStockHistoryAsync(_item.Id, _warehouse.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Processor().ConfirmBatchAsync([(7, Json(_item, _warehouse))], CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Fila 7").And.Contain("Vuelva a validar");
        _mediator.Verify(m => m.Send(It.IsAny<PostInitialBalanceCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cambio_de_sucursal_despues_del_preview_aborta_sin_escribir()
    {
        SetupConfirmable((_item, _warehouse));
        _branch.SetupGet(x => x.BranchId).Returns(Guid.NewGuid());

        var result = await Processor().ConfirmBatchAsync([(1, Json(_item, _warehouse))], CancellationToken.None);

        result.Error.Should().Contain("sucursal activa");
        _mediator.Verify(m => m.Send(It.IsAny<PostInitialBalanceCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Fallo_de_una_bodega_se_reporta_con_su_codigo()
    {
        SetupConfirmable((_item, _warehouse));
        _mediator.Setup(m => m.Send(It.IsAny<PostInitialBalanceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ERP.Application.Common.Result<StockAdjustmentDto>.ValidationFailure("ya tiene stock"));

        var result = await Processor().ConfirmBatchAsync([(1, Json(_item, _warehouse))], CancellationToken.None);

        result.Error.Should().Be("Bodega BOD-01: ya tiene stock");
    }

    [Fact]
    public async Task Confirmar_fila_por_fila_nunca_escribe()
    {
        var result = await Processor().ConfirmRowAsync(Json(_item, _warehouse), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _mediator.VerifyNoOtherCalls();
    }
}
