using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Infrastructure.InitialLoad;
using ERP.Domain.Exceptions;
using FluentAssertions;
using System.Globalization;

namespace ERP.Infrastructure.Tests.InitialLoad;

public sealed class ClosedXmlItemImportSheetReaderTests
{
    private readonly ClosedXmlItemImportSheetReader _reader = new();

    [Fact]
    public async Task Plantilla_roundtrip_contrato_explicito_y_texto()
    {
        var bytes = await _reader.BuildTemplateAsync(default);
        using var stream = new MemoryStream(bytes);
        var result = await _reader.ReadAsync(stream, default);
        result.Rows.Should().ContainSingle();
        var row = result.Rows[0];
        row.Keys.Should().Equal(ItemImportColumns.All);
        row.Should().NotContainKey("IVA");
        row[ItemImportColumns.SaleVatCode].Should().Be("2");
        row[ItemImportColumns.PurchaseVatCode].Should().Be("2");
        row[ItemImportColumns.BarcodeType1].Should().Be("Internal");
        row[ItemImportColumns.CategoryName].Should().Be("No aplica");
        row[ItemImportColumns.BrandName].Should().Be("No aplica");
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var instructions = string.Join(" ", workbook.Worksheet("Instrucciones").CellsUsed().Select(c => c.GetString()));
        instructions.Should().Contain("mayor que cero").And.Contain("nunca se sustituye").And.Contain("No se infiere");
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public async Task Encabezados_invalidos_se_rechazan(string mode)
    {
        using var workbook = new XLWorkbook(new MemoryStream(await _reader.BuildTemplateAsync(default)));
        var sheet = workbook.Worksheet("Productos");
        var column = ItemImportColumns.All.ToList().IndexOf(ItemImportColumns.SaleVatCode) + 1;
        sheet.Cell(1, column).Value = mode switch
        {
            "legacy" => "IVA",
            "missing" => "",
            _ => ItemImportColumns.PurchaseVatCode,
        };
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var act = () => _reader.ReadAsync(stream, default);
        await act.Should().ThrowAsync<DomainRuleViolationException>();
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("es-EC")]
    public async Task Numeros_excel_y_codigos_texto_no_dependen_de_cultura(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            using var workbook = new XLWorkbook(new MemoryStream(await _reader.BuildTemplateAsync(default)));
            var sheet = workbook.Worksheet("Productos");
            int Column(string name) => ItemImportColumns.All.ToList().IndexOf(name) + 1;
            sheet.Cell(2, Column(ItemImportColumns.Pvp)).Value = 9.99;
            sheet.Cell(2, Column(ItemImportColumns.Barcode1)).Value = "0012345678";
            sheet.Cell(2, Column(ItemImportColumns.UomCode)).Value = "07";
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            var row = (await _reader.ReadAsync(stream, default)).Rows[0];
            row[ItemImportColumns.Pvp].Should().Be("9.99");
            row[ItemImportColumns.Barcode1].Should().Be("0012345678");
            row[ItemImportColumns.UomCode].Should().Be("07");
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
