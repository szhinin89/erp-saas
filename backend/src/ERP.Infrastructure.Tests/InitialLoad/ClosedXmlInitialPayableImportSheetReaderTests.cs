using System.Globalization;
using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.Exceptions;
using ERP.Infrastructure.InitialLoad;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>IL-6A — contrato explícito de la plantilla de CxP Inicial y lectura invariante.</summary>
public sealed class ClosedXmlInitialPayableImportSheetReaderTests
{
    private readonly ClosedXmlInitialPayableImportSheetReader _reader = new();

    [Fact]
    public async Task Plantilla_roundtrip_contrato_explicito_y_texto()
    {
        var bytes = await _reader.BuildTemplateAsync(default);

        var result = await _reader.ReadAsync(new MemoryStream(bytes), default);

        var row = result.Rows.Should().ContainSingle().Subject;
        row.Keys.Should().Equal(InitialPayableImportColumns.All);
        row[InitialPayableImportColumns.IdentificationType].Should().Be("04", "el cero inicial no se pierde");
        row[InitialPayableImportColumns.DocumentType].Should().Be("01", "el tipo SRI conserva su cero inicial");
        row[InitialPayableImportColumns.DocumentNumber].Should().Be("001-001-000001234");
        row[InitialPayableImportColumns.Balance].Should().Be("150.75");
        row[InitialPayableImportColumns.Currency].Should().Be("USD");
        row[InitialPayableImportColumns.CutoffDate].Should().Be("2026-09-30");
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var data = workbook.Worksheet("CxP Inicial");
        for (var i = 1; i <= InitialPayableImportColumns.All.Count; i++)
            data.Column(i).Style.NumberFormat.Format.Should().Be("@");
        var instructions = string.Join(" ", workbook.Worksheet("Instrucciones").CellsUsed().Select(c => c.GetString()));
        instructions.Should().Contain("SALDO NETO PENDIENTE").And.Contain("nunca crea proveedores")
            .And.Contain("nunca genera retención").And.Contain("Tipo Documento").And.Contain("catálogo SRI")
            .And.Contain("sucursal activa").And.Contain("AAAA-MM-DD").And.Contain("punto decimal")
            .And.Contain("USD").And.Contain("fecha de apertura");
    }

    [Fact]
    public async Task Celdas_numericas_y_de_fecha_se_leen_invariantes_aunque_el_servidor_sea_es_EC()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("CxP Inicial");
        var columns = InitialPayableImportColumns.All;
        for (var i = 0; i < columns.Count; i++)
            sheet.Cell(1, i + 1).Value = columns[i];
        int Col(string name) => columns.ToList().IndexOf(name) + 1;
        sheet.Cell(2, Col(InitialPayableImportColumns.IdentificationType)).Value = "04";
        sheet.Cell(2, Col(InitialPayableImportColumns.DocumentType)).Value = 1;
        sheet.Cell(2, Col(InitialPayableImportColumns.DocumentNumber)).Value = "FAC-1";
        sheet.Cell(2, Col(InitialPayableImportColumns.IssueDate)).Value = new DateTime(2026, 8, 15);
        sheet.Cell(2, Col(InitialPayableImportColumns.Balance)).Value = 150.75;
        sheet.Cell(2, Col(InitialPayableImportColumns.CutoffDate)).Value = new DateTime(2026, 9, 30);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-EC");
        try
        {
            var row = (await _reader.ReadAsync(stream, default)).Rows.Single();

            row[InitialPayableImportColumns.Balance].Should().Be("150.75", "nunca '150,75' según la cultura del servidor");
            row[InitialPayableImportColumns.IssueDate].Should().Be("2026-08-15");
            row[InitialPayableImportColumns.CutoffDate].Should().Be("2026-09-30");
            row[InitialPayableImportColumns.Currency].Should().BeNull();
            row[InitialPayableImportColumns.DocumentType].Should().Be("1", "el processor detecta el cero perdido");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public async Task Encabezados_invalidos_se_rechazan(string mode)
    {
        var headers = InitialPayableImportColumns.All.ToList();
        if (mode == "missing") headers.Remove(InitialPayableImportColumns.DocumentType);
        if (mode == "duplicate") headers.Add(InitialPayableImportColumns.DocumentNumber);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("CxP Inicial");
        for (var i = 0; i < headers.Count; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var act = () => _reader.ReadAsync(stream, default);

        await act.Should().ThrowAsync<DomainRuleViolationException>().WithMessage(
            mode == "missing" ? "*Faltan encabezados*Tipo Documento*" : "*Encabezado duplicado*");
    }

    [Fact]
    public async Task Archivo_que_no_es_Excel_se_rechaza_con_mensaje_publico()
    {
        var act = () => _reader.ReadAsync(new MemoryStream([1, 2, 3]), default);

        await act.Should().ThrowAsync<DomainRuleViolationException>().WithMessage("*no es un Excel*");
    }
}
