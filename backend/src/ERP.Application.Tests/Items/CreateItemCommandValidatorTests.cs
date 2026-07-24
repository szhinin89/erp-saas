using ERP.Application.Items.UseCases.CreateItem;
using FluentAssertions;

namespace ERP.Application.Tests.Items;

public sealed class CreateItemCommandValidatorTests
{
    private static readonly CreateItemCommandValidator Validator = new();

    private static CreateItemCommand ValidCommand(
        IReadOnlyList<CreateItemBarcodeDto>? barcodes = null,
        IReadOnlyList<CreateItemSupplierCodeDto>? supplierCodes = null) => new(
        SKU: "SKU-001",
        ShortName: "Item de prueba",
        Description: "Descripción de prueba",
        ItemTypeId: Guid.NewGuid(),
        DefaultUomCode: "UNIT",
        CategoryNodeId: Guid.NewGuid(),
        BrandId: Guid.NewGuid(),
        Barcodes: barcodes ?? [new CreateItemBarcodeDto("7501234567890", "EAN13", true)],
        SupplierCodes: supplierCodes);

    [Fact]
    public void Command_valido_no_tiene_errores()
    {
        var result = Validator.Validate(ValidCommand());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CategoryNodeId_vacio_es_invalido()
    {
        var cmd = ValidCommand() with { CategoryNodeId = null };
        var result = Validator.Validate(cmd);
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateItemCommand.CategoryNodeId));
    }

    [Fact]
    public void BrandId_vacio_es_invalido()
    {
        var cmd = ValidCommand() with { BrandId = null };
        var result = Validator.Validate(cmd);
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateItemCommand.BrandId));
    }

    [Fact]
    public void Sin_barcodes_es_invalido()
    {
        var cmd = ValidCommand(barcodes: []);
        var result = Validator.Validate(cmd);
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateItemCommand.Barcodes));
    }

    [Fact]
    public void Dos_barcodes_principales_es_invalido()
    {
        var cmd = ValidCommand(barcodes:
        [
            new CreateItemBarcodeDto("111", "EAN13", true),
            new CreateItemBarcodeDto("222", "EAN13", true),
        ]);

        var result = Validator.Validate(cmd);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("exactamente un código de barras"));
    }

    [Fact]
    public void Ningun_barcode_principal_es_invalido()
    {
        var cmd = ValidCommand(barcodes:
        [
            new CreateItemBarcodeDto("111", "EAN13", false),
        ]);

        var result = Validator.Validate(cmd);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("exactamente un código de barras"));
    }

    [Fact]
    public void Barcodes_duplicados_es_invalido()
    {
        var cmd = ValidCommand(barcodes:
        [
            new CreateItemBarcodeDto("111", "EAN13", true),
            new CreateItemBarcodeDto("111", "EAN8", false),
        ]);

        var result = Validator.Validate(cmd);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("no pueden repetirse"));
    }

    [Fact]
    public void SupplierCodes_null_es_valido_porque_es_opcional()
    {
        var cmd = ValidCommand(supplierCodes: null);
        var result = Validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SupplierCodes_vacio_es_valido()
    {
        var cmd = ValidCommand(supplierCodes: []);
        var result = Validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Mas_de_un_supplierCode_principal_es_invalido()
    {
        var cmd = ValidCommand(supplierCodes:
        [
            new CreateItemSupplierCodeDto(Guid.NewGuid(), "SUP-1", true),
            new CreateItemSupplierCodeDto(Guid.NewGuid(), "SUP-2", true),
        ]);

        var result = Validator.Validate(cmd);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Solo un código de proveedor"));
    }

    [Fact]
    public void Un_solo_supplierCode_principal_es_valido()
    {
        var cmd = ValidCommand(supplierCodes:
        [
            new CreateItemSupplierCodeDto(Guid.NewGuid(), "SUP-1", true),
            new CreateItemSupplierCodeDto(Guid.NewGuid(), "SUP-2", false),
        ]);

        var result = Validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SupplierCode_sin_proveedor_es_invalido()
    {
        var cmd = ValidCommand(supplierCodes:
        [
            new CreateItemSupplierCodeDto(Guid.Empty, "SUP-1", false),
        ]);

        var result = Validator.Validate(cmd);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("El proveedor es obligatorio"));
    }

    [Fact]
    public void BaseSalePrice_negativo_es_invalido()
    {
        var cmd = ValidCommand() with { BaseSalePrice = -1m };
        var result = Validator.Validate(cmd);
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateItemCommand.BaseSalePrice));
    }

    [Fact]
    public void BaseSalePrice_nulo_es_valido()
    {
        var cmd = ValidCommand() with { BaseSalePrice = null };
        var result = Validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Codigos_de_impuesto_nulos_son_validos()
    {
        var cmd = ValidCommand() with { SaleVatCode = null, PurchaseVatCode = null, ExciseTaxCode = null };
        var result = Validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }
}
