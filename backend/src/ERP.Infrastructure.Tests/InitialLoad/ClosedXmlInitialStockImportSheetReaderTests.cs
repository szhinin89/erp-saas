using System.Globalization;
using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.Exceptions;
using ERP.Infrastructure.InitialLoad;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>IL-4A — contrato explícito de la plantilla de Inventario Inicial y lectura invariante.</summary>
public sealed class ClosedXmlInitialStockImportSheetReaderTests
{
    private readonly ClosedXmlInitialStockImportSheetReader _reader = new();

    [Fact]
    public async Task Plantilla_roundtrip_contrato_explicito_y_texto()
    {
        var bytes = await _reader.BuildTemplateAsync(default);

        var result = await _reader.ReadAsync(new MemoryStream(bytes), default);

        var row = result.Rows.Should().ContainSingle().Subject;
        row.Keys.Should().Equal(InitialStockImportColumns.All);
        row[InitialStockImportColumns.WarehouseCode].Should().Be("BOD-01");
        row[InitialStockImportColumns.UnitCost].Should().Be("3.50");
        row[InitialStockImportColumns.CutoffDate].Should().Be("2026-09-30");
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var data = workbook.Worksheet("Stock Inicial");
        for (var i = 1; i <= InitialStockImportColumns.All.Count; i++)
            data.Column(i).Style.NumberFormat.Format.Should().Be("@");
        var instructions = string.Join(" ", workbook.Worksheet("Instrucciones").CellsUsed().Select(c => c.GetString()));
        instructions.Should().Contain("punto decimal").And.Contain("sucursal activa").And.Contain("AAAA-MM-DD")
            .And.Contain("ajuste de inventario").And.Contain("lotes/series");
    }

    [Fact]
    public async Task Celdas_numericas_y_de_fecha_se_leen_invariantes_aunque_el_servidor_sea_es_EC()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Stock Inicial");
        for (var i = 0; i < InitialStockImportColumns.All.Count; i++)
            sheet.Cell(1, i + 1).Value = InitialStockImportColumns.All[i];
        sheet.Cell(2, 1).Value = "PROD-0001";
        sheet.Cell(2, 3).Value = "BOD-01";
        sheet.Cell(2, 4).Value = 1250;
        sheet.Cell(2, 5).Value = 3.5;
        sheet.Cell(2, 6).Value = new DateTime(2026, 9, 30);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-EC");
        try
        {
            var row = (await _reader.ReadAsync(stream, default)).Rows.Single();

            row[InitialStockImportColumns.Quantity].Should().Be("1250");
            row[InitialStockImportColumns.UnitCost].Should().Be("3.5", "nunca '3,5' según la cultura del servidor");
            row[InitialStockImportColumns.CutoffDate].Should().Be("2026-09-30");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("obsolete")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public async Task Encabezados_invalidos_se_rechazan(string mode)
    {
        var headers = InitialStockImportColumns.All.ToList();
        if (mode == "obsolete") headers.Add("Bodega");
        if (mode == "missing") headers.Remove(InitialStockImportColumns.CutoffDate);
        if (mode == "duplicate") headers.Add(InitialStockImportColumns.Sku);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Stock Inicial");
        for (var i = 0; i < headers.Count; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var act = () => _reader.ReadAsync(stream, default);

        await act.Should().ThrowAsync<DomainRuleViolationException>().WithMessage(mode switch
        {
            "obsolete" => "*ya no admite: Bodega*",
            "missing" => "*Faltan encabezados*Fecha de corte*",
            _ => "*Encabezado duplicado*",
        });
    }
}
