using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
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

public sealed class InitialStockImportProcessorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();

    private readonly Mock<IInitialStockImportSheetReader> _reader = new();
    private readonly Mock<IItemRepository> _itemRepo = new();
    private readonly Mock<IWarehouseRepository> _warehouseRepo = new();
    private readonly Mock<IInventoryAdjustmentReasonRepository> _reasonRepo = new();
    private readonly Mock<IOperationalContext> _ctx = new();
    private readonly Mock<IMediator> _mediator = new();

    private InitialStockImportProcessor BuildProcessor()
    {
        _ctx.SetupGet(x => x.TenantId).Returns(TenantId);
        _ctx.SetupGet(x => x.CompanyId).Returns(CompanyId);
        return new InitialStockImportProcessor(
            _reader.Object,
            _itemRepo.Object,
            _warehouseRepo.Object,
            _reasonRepo.Object,
            _ctx.Object,
            _mediator.Object
        );
    }

    private static Item AvailableItem(bool availableOnPos = true) =>
        Item.Create(
            TenantId,
            "PROD-0001",
            "Producto Uno",
            "Descripción del producto uno",
            Guid.NewGuid(),
            "19",
            ItemTaxConfig.Create(null, null, null),
            ItemSaleConfig.Create(true, null, false, availableOnPos, false, false, false),
            ItemStockConfig.Create(true, false, false, false, false, null, null),
            Guid.NewGuid()
        );

    private static Warehouse ActiveWarehouse() =>
        Warehouse.Create(
            TenantId,
            BranchId,
            "Bodega Principal",
            "BOD-01",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Guid.NewGuid(),
            CompanyId
        );

    private void SetupHappyPath(bool itemAvailableOnPos = true)
    {
        _itemRepo
            .Setup(x => x.ResolveByAnyCodeAsync("PROD-0001", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AvailableItem(itemAvailableOnPos));

        _warehouseRepo
            .Setup(x =>
                x.GetAsync(TenantId, true, "Bodega Principal", null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([ActiveWarehouse()]);
    }

    private static Dictionary<string, string?> ValidRow() =>
        new()
        {
            [InitialStockImportColumns.Sku] = "PROD-0001",
            [InitialStockImportColumns.Barcode] = null,
            [InitialStockImportColumns.Warehouse] = "Bodega Principal",
            [InitialStockImportColumns.Quantity] = "100",
            [InitialStockImportColumns.UnitCost] = "3.50",
            [InitialStockImportColumns.CutoffDate] = null,
            [InitialStockImportColumns.Observation] = null,
        };

    [Fact]
    public async Task Fila_valida_no_genera_issues_bloqueantes()
    {
        SetupHappyPath();
        var processor = BuildProcessor();

        var result = await processor.ValidateRowAsync(1, ValidRow(), false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Sku_inexistente_es_error_bloqueante()
    {
        _itemRepo
            .Setup(x => x.ResolveByAnyCodeAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Item?)null);
        _warehouseRepo
            .Setup(x => x.GetAsync(TenantId, true, "Bodega Principal", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ActiveWarehouse()]);
        var processor = BuildProcessor();

        var result = await processor.ValidateRowAsync(1, ValidRow(), false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task Bodega_inexistente_es_error_bloqueante()
    {
        _itemRepo
            .Setup(x => x.ResolveByAnyCodeAsync("PROD-0001", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AvailableItem());
        _warehouseRepo
            .Setup(x => x.GetAsync(TenantId, true, "Bodega Principal", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var processor = BuildProcessor();

        var result = await processor.ValidateRowAsync(1, ValidRow(), false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "WAREHOUSE_NOT_FOUND");
    }

    [Fact]
    public async Task Cantidad_invalida_es_error_bloqueante()
    {
        SetupHappyPath();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[InitialStockImportColumns.Quantity] = "-5";

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "INVALID_QUANTITY");
    }

    [Fact]
    public async Task Costo_unitario_faltante_es_error_bloqueante()
    {
        // ExecuteStockAdjustmentCommandHandler ya exige costo > 0 para Ingreso — nunca es
        // advertencia en este importador.
        SetupHappyPath();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[InitialStockImportColumns.UnitCost] = null;

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "MISSING_REQUIRED_FIELD" && i.FieldName == InitialStockImportColumns.UnitCost);
    }

    [Fact]
    public async Task Costo_unitario_cero_es_error_bloqueante()
    {
        SetupHappyPath();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[InitialStockImportColumns.UnitCost] = "0";

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "INVALID_UNIT_COST");
    }

    [Fact]
    public async Task Fecha_de_corte_invalida_es_error_bloqueante()
    {
        SetupHappyPath();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[InitialStockImportColumns.CutoffDate] = "no-es-una-fecha";

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "INVALID_CUTOFF_DATE");
    }

    [Fact]
    public async Task Fecha_de_corte_valida_genera_warning_informativo_no_bloqueante()
    {
        SetupHappyPath();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[InitialStockImportColumns.CutoffDate] = "2026-01-01";

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().ContainSingle(i =>
            i.Code == "CUTOFF_DATE_NOT_APPLIED" && i.Severity == ImportSeverity.Warning
        );
    }

    [Fact]
    public async Task Item_no_disponible_en_pos_genera_warning_no_bloqueante()
    {
        SetupHappyPath(itemAvailableOnPos: false);
        var processor = BuildProcessor();

        var result = await processor.ValidateRowAsync(1, ValidRow(), false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().ContainSingle(i =>
            i.Code == "ITEM_NOT_AVAILABLE_ON_POS" && i.Severity == ImportSeverity.Warning
        );
    }

    [Fact]
    public async Task Duplicado_item_bodega_en_la_misma_carga_es_error_bloqueante_en_la_segunda_fila()
    {
        SetupHappyPath();
        var processor = BuildProcessor();

        var first = await processor.ValidateRowAsync(1, ValidRow(), false, CancellationToken.None);
        var second = await processor.ValidateRowAsync(2, ValidRow(), false, CancellationToken.None);

        first.HasBlockingIssue.Should().BeFalse();
        second.HasBlockingIssue.Should().BeTrue();
        second.Issues.Should().ContainSingle(i => i.Code == "DUPLICATE_ITEM_WAREHOUSE_IN_ROW");
    }

    [Fact]
    public async Task Observacion_vacia_no_genera_ningun_issue()
    {
        SetupHappyPath();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[InitialStockImportColumns.Observation] = null;

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.Issues.Should().NotContain(i => i.FieldName == InitialStockImportColumns.Observation);
    }
}
