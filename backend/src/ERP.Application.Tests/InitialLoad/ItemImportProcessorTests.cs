using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.InitialLoad.UseCases.ValidateImportBatch;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using ERP.Domain.MasterData.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Items.DTOs;
using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
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
    private static readonly Guid CompanyId = Guid.NewGuid();

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
        _ctx.SetupGet(x => x.CompanyId).Returns(CompanyId);
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

        _sri.Setup(x =>
                x.ResolveUomsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new Dictionary<string, SriUomInfo> { ["19"] = new("UN", "Unidad") });

        _sri.Setup(x =>
                x.ResolveVatRatesAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
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

        _catalogRepo.Setup(x => x.BarcodeTypeExistsAndActiveAsync("Internal", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var brand = Brand.Create(TenantId, "MARCA-001", "Marca Uno", Guid.NewGuid());
        _catalogRepo
            .Setup(x => x.GetBrandsAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([brand]);

        _itemRepo
            .Setup(x =>
                x.ExistsBySkuAsync(
                    It.IsAny<string>(),
                    TenantId,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        _itemRepo
            .Setup(x =>
                x.BarcodeExistsAsync(It.IsAny<string>(), TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);
    }

    private static Dictionary<string, string?> ValidRow() =>
        new()
        {
            [ItemImportColumns.Sku] = "PROD-0001",
            [ItemImportColumns.Name] = "Producto Válido",
            [ItemImportColumns.ItemTypeCode] = "Physical",
            [ItemImportColumns.UomCode] = "19",
            [ItemImportColumns.SaleVatCode] = "2",
            [ItemImportColumns.CategoryName] = "Bebidas",
            [ItemImportColumns.BrandName] = "Marca Uno",
            [ItemImportColumns.Barcode1] = "7861234567890",
            [ItemImportColumns.BarcodeType1] = "Internal",
            [ItemImportColumns.PurchaseVatCode] = "2",
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
    public async Task Pos_si_sin_pvp_bloquea_y_conserva_eleccion()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.Pvp] = null;

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "POS_REQUIRES_PRICE");
        result.ParsedDataJson.Should().Contain("\"IsAvailableOnPOS\":true");
    }

    [Fact]
    public async Task Pvp_invalido_bloquea_y_conserva_eleccion()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.Pvp] = "no-es-un-numero";

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "INVALID_PRICE");
        result.ParsedDataJson.Should().Contain("\"IsAvailableOnPOS\":true");
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
        result
            .Issues.Should()
            .ContainSingle(i =>
                i.Code == "MISSING_REQUIRED_FIELD" && i.FieldName == ItemImportColumns.Name
            );
    }

    [Fact]
    public async Task Unidad_base_invalida_es_error_bloqueante()
    {
        SetupHappyPathCatalogs();
        _sri.Setup(x =>
                x.ResolveUomsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())
            )
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
                x.ExistsBySkuAsync(
                    It.IsAny<string>(),
                    TenantId,
                    null,
                    It.IsAny<CancellationToken>()
                )
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
            .Setup(x =>
                x.BarcodeExistsAsync(It.IsAny<string>(), TenantId, CompanyId, It.IsAny<CancellationToken>())
            )
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
        row[ItemImportColumns.BarcodeType1] = null;

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result
            .Issues.Should()
            .ContainSingle(i =>
                i.Code == "MISSING_REQUIRED_FIELD" && i.FieldName == ItemImportColumns.Barcode1
            );
    }

    [Fact]
    public async Task Codigos_de_barras_repetidos_en_la_misma_fila_es_error_bloqueante()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.Barcode2] = row[ItemImportColumns.Barcode1];
        row[ItemImportColumns.BarcodeType2] = "Internal";

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

        var result = await processor.ValidateRowAsync(
            1,
            row,
            autoCreateCatalogValues: false,
            CancellationToken.None
        );

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

        var result = await processor.ValidateRowAsync(
            1,
            row,
            autoCreateCatalogValues: true,
            CancellationToken.None
        );

        result.HasBlockingIssue.Should().BeFalse();
        result
            .Issues.Should()
            .ContainSingle(i =>
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

        var result = await processor.ValidateRowAsync(
            1,
            row,
            autoCreateCatalogValues: false,
            CancellationToken.None
        );

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

        var result = await processor.ValidateRowAsync(
            1,
            row,
            autoCreateCatalogValues: true,
            CancellationToken.None
        );

        result.HasBlockingIssue.Should().BeTrue();
        result
            .Issues.Should()
            .ContainSingle(i =>
                i.Code == "MISSING_REQUIRED_FIELD" && i.FieldName == ItemImportColumns.CategoryName
            );
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
        result
            .Issues.Should()
            .ContainSingle(i =>
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
        row[ItemImportColumns.SaleVatCode] = null;

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.Issues.Should().NotContain(i => i.FieldName == ItemImportColumns.SaleVatCode);
    }

    [Fact]
    public async Task Iva_invalido_es_error_bloqueante()
    {
        SetupHappyPathCatalogs();
        _sri.Setup(x =>
                x.ResolveVatRatesAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<string, SriVatInfo>());
        var processor = BuildProcessor();
        var row = ValidRow();
        row[ItemImportColumns.SaleVatCode] = "99";

        var result = await processor.ValidateRowAsync(1, row, false, CancellationToken.None);

        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Code == "INVALID_VAT_CODE" && i.FieldName == ItemImportColumns.SaleVatCode);
        result.Issues.Should().ContainSingle(i => i.Code == "INVALID_VAT_CODE" && i.FieldName == ItemImportColumns.PurchaseVatCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData("SÍ")]
    [InlineData("quizás")]
    public async Task Pos_solo_acepta_si_no(string? value)
    {
        SetupHappyPathCatalogs();
        var row = ValidRow();
        row[ItemImportColumns.AvailableOnPos] = value;
        var result = await BuildProcessor().ValidateRowAsync(1, row, false, default);
        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "INVALID_POS");
    }

    [Theory]
    [InlineData("SI", null, true)]
    [InlineData("SI", "0", true)]
    [InlineData("SI", "-1", true)]
    [InlineData("SI", "texto", true)]
    [InlineData("SI", "0.01", false)]
    [InlineData("SI", "1E-7", false)]
    [InlineData("NO", null, false)]
    [InlineData("NO", "0", false)]
    [InlineData("NO", "texto", true)]
    [InlineData("NO", "-1", true)]
    public async Task Matriz_pos_pvp(string pos, string? price, bool blocked)
    {
        SetupHappyPathCatalogs();
        var row = ValidRow();
        row[ItemImportColumns.AvailableOnPos] = pos;
        row[ItemImportColumns.Pvp] = price;
        var result = await BuildProcessor().ValidateRowAsync(1, row, false, default);
        result.HasBlockingIssue.Should().Be(blocked);
        JsonSerializer.Deserialize<ParsedItemRow>(result.ParsedDataJson)!.IsAvailableOnPOS.Should().Be(pos == "SI");
    }

    [Theory]
    [InlineData(ItemImportColumns.BarcodeType1, null)]
    [InlineData(ItemImportColumns.BarcodeType1, "EAN13")]
    [InlineData(ItemImportColumns.Sku, "no permitido!")]
    [InlineData(ItemImportColumns.Sku, "LONG_SKU")]
    [InlineData(ItemImportColumns.Observations, "LONG_OBSERVATIONS")]
    [InlineData(ItemImportColumns.Barcode1, "LONG_BARCODE")]
    [InlineData(ItemImportColumns.UomCode, "LONG_UOM")]
    [InlineData(ItemImportColumns.CategoryName, "LONG_CATEGORY")]
    [InlineData(ItemImportColumns.BrandName, "LONG_BRAND")]
    [InlineData(ItemImportColumns.PurchaseVatCode, "99")]
    public async Task Preview_bloquea_restricciones(string field, string? value)
    {
        SetupHappyPathCatalogs();
        var row = ValidRow();
        row[field] = value switch
        {
            "LONG_SKU" => new string('A', 51),
            "LONG_OBSERVATIONS" => new string('A', 501),
            "LONG_BARCODE" => new string('A', 101),
            "LONG_UOM" => new string('A', 11),
            "LONG_CATEGORY" or "LONG_BRAND" => new string('A', 121),
            _ => value,
        };
        var result = await BuildProcessor().ValidateRowAsync(1, row, true, default);
        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Severity == ImportSeverity.Error && i.FieldName == field);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Categoria_hoja_y_rama_activa(bool disabledAncestor)
    {
        SetupHappyPathCatalogs();
        _categoryRepo.Setup(x => x.HasActiveChildrenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(!disabledAncestor);
        _categoryRepo.Setup(x => x.AnyAncestorDisabledAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(disabledAncestor);
        var result = await BuildProcessor().ValidateRowAsync(1, ValidRow(), false, default);
        result.Issues.Should().Contain(i => i.Code == "INVALID_CATEGORY");
    }

    [Theory]
    [InlineData(ItemImportColumns.CategoryName)]
    [InlineData(ItemImportColumns.BrandName)]
    public async Task Vacio_no_se_convierte_a_no_aplica(string field)
    {
        SetupHappyPathCatalogs();
        var row = ValidRow();
        row[field] = " ";
        var result = await BuildProcessor().ValidateRowAsync(1, row, true, default);
        result.HasBlockingIssue.Should().BeTrue();
        result.ParsedDataJson.Should().NotContain("No aplica");
        _mediator.Verify(x => x.Send(It.IsAny<IRequest<Result<ItemDto>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task No_aplica_explicito_y_iva_vacios_no_inventan_defaults()
    {
        SetupHappyPathCatalogs();
        _categoryRepo.Setup(x => x.GetAllAsync(TenantId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ItemCategoryNode.Create(TenantId, "NO_APLICA", "No aplica", CategoryNodeLevel.Category, Guid.NewGuid())]);
        _catalogRepo.Setup(x => x.GetBrandsAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Brand.Create(TenantId, "NO_APLICA", "No aplica", Guid.NewGuid())]);
        var row = ValidRow();
        row[ItemImportColumns.CategoryName] = row[ItemImportColumns.BrandName] = "No aplica";
        row[ItemImportColumns.SaleVatCode] = row[ItemImportColumns.PurchaseVatCode] = null;
        var result = await BuildProcessor().ValidateRowAsync(1, row, false, default);
        result.HasBlockingIssue.Should().BeFalse();
        var parsed = JsonSerializer.Deserialize<ParsedItemRow>(result.ParsedDataJson)!;
        parsed.SaleVatCode.Should().BeNull();
        parsed.PurchaseVatCode.Should().BeNull();
    }

    [Fact]
    public async Task Tipos_y_iva_se_conservan_en_confirm_mapping()
    {
        SetupHappyPathCatalogs();
        _catalogRepo.Setup(x => x.BarcodeTypeExistsAndActiveAsync("EAN13", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _sri.Setup(x => x.ResolveVatRatesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, SriVatInfo> { ["2"] = new("Venta", 15), ["0"] = new("Compra", 0) });
        var row = ValidRow();
        row[ItemImportColumns.BarcodeType1] = "EAN13";
        row[ItemImportColumns.Barcode2] = "ABC";
        row[ItemImportColumns.BarcodeType2] = "Internal";
        row[ItemImportColumns.PurchaseVatCode] = "0";
        var processor = BuildProcessor();
        var validation = await processor.ValidateRowAsync(1, row, false, default);
        validation.HasBlockingIssue.Should().BeFalse();
        CreateItemCommand? sent = null;
        _mediator.Setup(x => x.Send(It.IsAny<CreateItemCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<ItemDto>>, CancellationToken>((command, _) => sent = (CreateItemCommand)command)
            .ReturnsAsync(Result<ItemDto>.ValidationFailure("Solo captura del mapeo"));
        await processor.ConfirmRowAsync(validation.ParsedDataJson, default);
        sent.Should().NotBeNull();
        sent!.SaleVatCode.Should().Be("2");
        sent.PurchaseVatCode.Should().Be("0");
        sent.Barcodes.Select(b => b.BarcodeType).Should().Equal("EAN13", "Internal");
        sent.Barcodes.Select(b => b.IsPrimary).Should().Equal(true, false);
    }

    [Fact]
    public async Task Duplicados_archivo_marcan_todas_las_filas_y_no_contaminan_otro_lote()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var valid = await processor.ValidateRowAsync(1, ValidRow(), false, default);
        var row = JsonSerializer.Deserialize<ParsedItemRow>(valid.ParsedDataJson)!;
        var supplier = Guid.NewGuid();
        RowValidationResult Result(ParsedItemRow data) => valid with { ParsedDataJson = JsonSerializer.Serialize(data) };
        var rows = new[]
        {
            Result(row with { SKU = " abc ", SupplierId = supplier, SupplierItemCode = " p-01 " }),
            Result(row with { SKU = "ABC", Barcodes = [new(" 7861234567890 ", "Internal")], SupplierId = supplier, SupplierItemCode = "P-01" }),
            Result(row with { SKU = "OTHER", Barcodes = [new("unique", "Internal")], SupplierId = Guid.NewGuid(), SupplierItemCode = "P-01" }),
        };
        var results = processor.ValidateBatch(rows);
        foreach (var result in results.Take(2))
        {
            result.HasBlockingIssue.Should().BeTrue();
            result.Issues.Select(i => i.Code).Should().Contain(["DUPLICATE_SKU_IN_FILE", "DUPLICATE_BARCODE_IN_FILE", "DUPLICATE_SUPPLIER_CODE_IN_FILE"]);
        }
        results[2].HasBlockingIssue.Should().BeFalse();
        processor.ValidateBatch([rows[0]])[0].HasBlockingIssue.Should().BeFalse();
        valid.HasBlockingIssue.Should().BeFalse();
    }

    [Fact]
    public async Task Tipo_sin_codigo_es_error_y_barcode_con_ceros_no_se_transforma()
    {
        SetupHappyPathCatalogs();
        var row = ValidRow();
        row[ItemImportColumns.Barcode1] = "001234";
        row[ItemImportColumns.BarcodeType3] = "Internal";
        var result = await BuildProcessor().ValidateRowAsync(1, row, false, default);
        result.Issues.Should().Contain(i => i.FieldName == ItemImportColumns.Barcode3 && i.Severity == ImportSeverity.Error);
        JsonSerializer.Deserialize<ParsedItemRow>(result.ParsedDataJson)!.Barcodes[0].Code.Should().Be("001234");
    }

    [Fact]
    public async Task Handler_persiste_duplicados_en_preview_y_no_envia_comandos_de_negocio()
    {
        SetupHappyPathCatalogs();
        var processor = BuildProcessor();
        var user = Guid.NewGuid();
        _ctx.SetupGet(x => x.UserId).Returns(user);
        var batch = ImportBatch.Create(TenantId, CompanyId, ImportType.Items, user);
        batch.AttachFile("file.xlsx", "file.xlsx", 1, user);
        batch.MarkUploaded(user);
        var batches = new Mock<IImportBatchRepository>();
        var rows = new Mock<IImportBatchRowRepository>();
        var issues = new Mock<IImportBatchIssueRepository>();
        var files = new Mock<IFileStorage>();
        batches.Setup(x => x.GetByIdAsync(batch.Id, TenantId, CompanyId, It.IsAny<CancellationToken>())).ReturnsAsync(batch);
        files.Setup(x => x.GetAsync("file.xlsx", It.IsAny<CancellationToken>())).ReturnsAsync(new MemoryStream([1]));
        var first = ValidRow();
        var second = ValidRow();
        second[ItemImportColumns.Sku] = " prod-0001 ";
        second[ItemImportColumns.Barcode1] = " 7861234567890 ";
        _reader.Setup(x => x.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportReadResult([first, second]));
        var recordedRows = new List<ImportBatchRow>();
        var recordedIssues = new List<ImportBatchIssue>();
        rows.Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<ImportBatchRow>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ImportBatchRow>, CancellationToken>((values, _) => recordedRows.AddRange(values));
        issues.Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<ImportBatchIssue>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ImportBatchIssue>, CancellationToken>((values, _) => recordedIssues.AddRange(values));
        var handler = new ValidateImportBatchHandler(
            batches.Object, rows.Object, issues.Object, files.Object,
            new Dictionary<ImportType, IImportProcessor> { [ImportType.Items] = processor }, _ctx.Object,
            NullLogger<ValidateImportBatchHandler>.Instance);
        var result = await handler.Handle(new ValidateImportBatchCommand(batch.Id), default);
        result.IsSuccess.Should().BeTrue();
        result.Value!.ValidRows.Should().Be(0);
        result.Value.IssueRows.Should().Be(2);
        recordedRows.Should().OnlyContain(r => r.HasBlockingIssue);
        recordedIssues.Should().HaveCount(4);
        _mediator.Verify(x => x.Send(It.IsAny<IRequest<Result<ItemDto>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("COD", true)]
    [InlineData("LONG", false)]
    public async Task Codigo_proveedor_bd_y_longitud_se_validan(string code, bool exists)
    {
        SetupHappyPathCatalogs();
        var supplier = BusinessPartner.Create(TenantId, "04", "1790016919001", 2, "Proveedor Uno", Guid.NewGuid());
        _bpRepo.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<bool?>(),
            It.IsAny<ERP.Domain.MasterData.Enums.RoleType[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([supplier]);
        var row = ValidRow();
        row[ItemImportColumns.SupplierQuery] = "Proveedor Uno";
        row[ItemImportColumns.SupplierItemCode] = code == "LONG" ? new string('A', 101) : code;
        _itemRepo.Setup(x => x.SupplierCodeExistsAsync(supplier.Id, row[ItemImportColumns.SupplierItemCode]!, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(exists);
        var result = await BuildProcessor().ValidateRowAsync(1, row, false, default);
        result.HasBlockingIssue.Should().BeTrue();
        result.Issues.Should().Contain(i => i.FieldName == ItemImportColumns.SupplierItemCode && i.Severity == ImportSeverity.Error);
    }
}
