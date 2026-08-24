using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Enums;
using ERP.Domain.Modules.Items.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.InitialLoad;

public sealed class ItemImportProcessorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly Mock<IItemImportSheetReader> _reader = new();
    private readonly Mock<IItemRepository> _itemRepo = new();
    private readonly Mock<IItemTypeRepository> _itemTypeRepo = new();
    private readonly Mock<ICategoryNodeRepository> _categoryRepo = new();
    private readonly Mock<IItemCatalogRepository> _catalogRepo = new();
    private readonly Mock<IBusinessPartnerRepository> _bpRepo = new();
    private readonly Mock<ISriCatalogResolver> _sri = new();
    private readonly Mock<IOperationalContext> _ctx = new();
    private readonly Mock<IMediator> _mediator = new();

    private ItemImportProcessor BuildProcessor()
    {
        _ctx.SetupGet(x => x.TenantId).Returns(TenantId);
        return new ItemImportProcessor(
            _reader.Object,
            _itemRepo.Object,
            _itemTypeRepo.Object,
            _categoryRepo.Object,
            _catalogRepo.Object,
            _bpRepo.Object,
            _sri.Object,
            _ctx.Object,
            _mediator.Object
        );
    }

    private void SetupHappyPathCatalogs()
    {
        var itemType = ItemTypeDefinition.Create(TenantId, "Physical", "Físico", 1, Guid.NewGuid());
        _itemTypeRepo
            .Setup(x => x.GetByCodeAsync(TenantId, "Physical", It.IsAny<CancellationToken>()))
            .ReturnsAsync(itemType);

        _sri.Setup(x => x.ResolveUomsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, SriUomInfo> { ["19"] = new("UN", "Unidad") });

        _sri.Setup(x => x.ResolveVatRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, SriVatInfo> { ["2"] = new("IVA 15%", 15m) });

        var category = ItemCategoryNode.Create(
            TenantId,
            "CAT-001",
            "Bebidas",
            CategoryNodeLevel.Category,
            Guid.NewGuid()
        );
        _categoryRepo
            .Setup(x => x.GetAllAsync(TenantId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([category]);

        var brand = Brand.Create(TenantId, "MARCA-001", "Marca Uno", Guid.NewGuid());
        _catalogRepo
            .Setup(x => x.GetBrandsAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([brand]);

        _itemRepo
            .Setup(x =>
                x.ExistsBySkuAsync(It.IsAny<string>(), TenantId, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
        _itemRepo
            .Setup(x => x.BarcodeExistsAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private static Dictionary<string, string?> ValidRow() =>
        new()
        {
            [ItemImportColumns.Sku] = "PROD-0001",
            [ItemImportColumns.Name] = "Producto Válido",
            [ItemImportColumns.ItemTypeCode] = "Physical",
            [ItemImportColumns.UomCode] = "19",
            [ItemImportColumns.VatCode] = "2",
            [ItemImportColumns.CategoryName] = "Bebidas",
            [ItemImportColumns.BrandName] = "Marca Uno",
            [ItemImportColumns.Barcode1] = "7861234567890",
            [ItemImportColumns.Barcode2] = null,
            [ItemImportColumns.Barcode3] = null,
            [ItemImportColumns.Pvp] = "9.99",
            [ItemImportColumns.AvailableOnPos] = "SI",
            [ItemImportColumns.SupplierQuery] = null,
            [ItemImportColumns.SupplierItemCode] = null,
            [ItemImportColumns.Cost] = null,
            [ItemImportColumns.Observations] = "Nota",
        };

    [Fact]
    public async Task Fila_valida_completa_no_genera_issues_y_queda_disponible_en_pos()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();

        var result = await processor.ValidateRowAsync(1, ValidRow(), false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().BeEmpty();
        result.ParsedDataJson.Should().Contain("\"IsAvailableOnPOS\":true");
    }

    [Fact]
    public async Task Sin_pvp_genera_warning_y_fuerza_no_disponible_en_pos()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.Pvp] = null;

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().ContainSingle(i =>
            i.Code == "MISSING_SALE_PRICE" && i.Severity == ImportSeverity.Warning
        );
        result.ParsedDataJson.Should().Contain("\"IsAvailableOnPOS\":false");
    }

    [Fact]
    public async Task Pvp_invalido_no_bloquea_y_reporta_que_no_se_aplico()
    {
        // Rediseño "importación inteligente": un PVP con formato inválido nunca bloquea la fila —
        // el producto se importa sin precio, igual que si el PVP viniera vacío.
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.Pvp] = "no-es-un-numero";

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().ContainSingle(i =>
            i.Code == "PRICE_NOT_APPLIED" && i.Severity == ImportSeverity.Warning
        );
        result.ParsedDataJson.Should().Contain("\"IsAvailableOnPOS\":false");
    }

    [Fact]
    public async Task Sin_nombre_es_error_bloqueante()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.Name] = null;

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "MISSING_REQUIRED_FIELD" && i.FieldName == ItemImportColumns.Name);
    }

    [Fact]
    public async Task Unidad_base_invalida_es_error_bloqueante()
    {
        SetupHappyPathCatalogs();
        _sri.Setup(x => x.ResolveUomsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, SriUomInfo>());
        var processor = BuildProcessor();

        var result = await processor.ValidateRowAsync(1, ValidRow(), false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "INVALID_UOM");
    }

    [Fact]
    public async Task Sku_duplicado_es_error_bloqueante()
    {
        SetupHappyPathCatalogs();
        _itemRepo
            .Setup(x =>
                x.ExistsBySkuAsync(It.IsAny<string>(), TenantId, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);
        var processor = BuildProcessor();

        var result = await processor.ValidateRowAsync(1, ValidRow(), false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "DUPLICATE_SKU");
    }

    [Fact]
    public async Task Codigo_de_barras_duplicado_es_error_bloqueante()
    {
        SetupHappyPathCatalogs();
        _itemRepo
            .Setup(x => x.BarcodeExistsAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var processor = BuildProcessor();

        var result = await processor.ValidateRowAsync(1, ValidRow(), false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "DUPLICATE_BARCODE");
    }

    [Fact]
    public async Task Sin_ningun_codigo_de_barras_es_error_bloqueante()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.Barcode1] = null;

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "MISSING_REQUIRED_FIELD" && i.FieldName == ItemImportColumns.Barcode1);
    }

    [Fact]
    public async Task Codigos_de_barras_repetidos_en_la_misma_fila_es_error_bloqueante()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.Barcode2] = row[ItemImportColumns.Barcode1];

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "DUPLICATE_BARCODE_IN_ROW");
    }

    [Fact]
    public async Task Categoria_inexistente_sin_autocrear_es_error_bloqueante()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.CategoryName] = "Categoría Nueva";

        var result = await processor.ValidateRowAsync(1, row, autoCreateCatalogValues: false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "CATEGORY_NOT_FOUND");
    }

    [Fact]
    public async Task Categoria_inexistente_con_autocrear_es_warning_no_bloqueante()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.CategoryName] = "Categoría Nueva";

        var result = await processor.ValidateRowAsync(1, row, autoCreateCatalogValues: true, CancellationToken.None);

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().ContainSingle(i =>
            i.Code == "CATEGORY_WILL_BE_CREATED" && i.Severity == ImportSeverity.Warning
        );
    }

    [Fact]
    public async Task Marca_inexistente_sin_autocrear_es_error_bloqueante()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.BrandName] = "Marca Nueva";

        var result = await processor.ValidateRowAsync(1, row, autoCreateCatalogValues: false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "BRAND_NOT_FOUND");
    }

    [Fact]
    public async Task Sin_categoria_es_error_bloqueante_incluso_con_autocrear()
    {
        // Columna vacía nunca dispara autocreación (regla explícita: "no inventar 'No aplica'
        // automáticamente si la columna viene vacía") — solo un valor presente pero inexistente
        // califica para autocrear.
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.CategoryName] = null;

        var result = await processor.ValidateRowAsync(1, row, autoCreateCatalogValues: true, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "MISSING_REQUIRED_FIELD" && i.FieldName == ItemImportColumns.CategoryName);
    }

    [Fact]
    public async Task Costo_presente_genera_warning_informativo_no_bloqueante()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.Cost] = "5.50";

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Code == "COST_NOT_IMPORTED");
    }

    [Fact]
    public async Task Proveedor_sin_coincidencia_unica_genera_warning_y_no_vincula()
    {
        SetupHappyPathCatalogs();
        _bpRepo
            .Setup(x =>
                x.SearchAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool?>(),
                    It.IsAny<ERP.Domain.MasterData.Enums.RoleType[]>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.SupplierQuery] = "Proveedor Inexistente";
        row[ItemImportColumns.SupplierItemCode] = "COD-123";

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeFalse();
        result.Issues.Should().ContainSingle(i =>
            i.Code == "SUPPLIER_NOT_LINKED" && i.Severity == ImportSeverity.Warning
        );
        result.ParsedDataJson.Should().Contain("\"SupplierId\":null");
    }

    [Fact]
    public async Task Iva_vacio_es_valido_sin_issue()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.VatCode] = null;

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.Issues.Should().NotContain(i => i.FieldName == ItemImportColumns.VatCode);
    }

    [Fact]
    public async Task Iva_invalido_es_error_bloqueante()
    {
        SetupHappyPathCatalogs();
        _sri.Setup(x => x.ResolveVatRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, SriVatInfo>());
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.VatCode] = "99";

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "INVALID_VAT_CODE");
    }
}
