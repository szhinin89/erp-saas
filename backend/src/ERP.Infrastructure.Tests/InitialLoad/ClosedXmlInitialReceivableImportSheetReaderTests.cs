using System.Globalization;
using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.Exceptions;
using ERP.Infrastructure.InitialLoad;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>IL-5A — contrato explícito de la plantilla de CxC Inicial y lectura invariante.</summary>
public sealed class ClosedXmlInitialReceivableImportSheetReaderTests
{
    private readonly ClosedXmlInitialReceivableImportSheetReader _reader = new();

    [Fact]
    public async Task Plantilla_roundtrip_contrato_explicito_y_texto()
    {
        var bytes = await _reader.BuildTemplateAsync(default);

        var result = await _reader.ReadAsync(new MemoryStream(bytes), default);

        var row = result.Rows.Should().ContainSingle().Subject;
        row.Keys.Should().Equal(InitialReceivableImportColumns.All);
        row[InitialReceivableImportColumns.IdentificationType].Should().Be("04", "el cero inicial no se pierde");
        row[InitialReceivableImportColumns.DocumentNumber].Should().Be("001-001-000001234");
        row[InitialReceivableImportColumns.Balance].Should().Be("150.75");
        row[InitialReceivableImportColumns.Currency].Should().Be("USD");
        row[InitialReceivableImportColumns.CutoffDate].Should().Be("2026-09-30");
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var data = workbook.Worksheet("CxC Inicial");
        for (var i = 1; i <= InitialReceivableImportColumns.All.Count; i++)
            data.Column(i).Style.NumberFormat.Format.Should().Be("@");
        var instructions = string.Join(" ", workbook.Worksheet("Instrucciones").CellsUsed().Select(c => c.GetString()));
        instructions.Should().Contain("SALDO PENDIENTE").And.Contain("nunca crea clientes")
            .And.Contain("sucursal activa").And.Contain("AAAA-MM-DD").And.Contain("punto decimal")
            .And.Contain("USD").And.Contain("fecha de apertura");
    }

    [Fact]
    public async Task Celdas_numericas_y_de_fecha_se_leen_invariantes_aunque_el_servidor_sea_es_EC()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("CxC Inicial");
        var columns = InitialReceivableImportColumns.All;
        for (var i = 0; i < columns.Count; i++)
            sheet.Cell(1, i + 1).Value = columns[i];
        int Col(string name) => columns.ToList().IndexOf(name) + 1;
        sheet.Cell(2, Col(InitialReceivableImportColumns.IdentificationType)).Value = "04";
        sheet.Cell(2, Col(InitialReceivableImportColumns.DocumentNumber)).Value = "FAC-1";
        sheet.Cell(2, Col(InitialReceivableImportColumns.IssueDate)).Value = new DateTime(2026, 8, 15);
        sheet.Cell(2, Col(InitialReceivableImportColumns.Balance)).Value = 150.75;
        sheet.Cell(2, Col(InitialReceivableImportColumns.CutoffDate)).Value = new DateTime(2026, 9, 30);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-EC");
        try
        {
            var row = (await _reader.ReadAsync(stream, default)).Rows.Single();

            row[InitialReceivableImportColumns.Balance].Should().Be("150.75", "nunca '150,75' según la cultura del servidor");
            row[InitialReceivableImportColumns.IssueDate].Should().Be("2026-08-15");
            row[InitialReceivableImportColumns.CutoffDate].Should().Be("2026-09-30");
            row[InitialReceivableImportColumns.Currency].Should().BeNull();
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
        var headers = InitialReceivableImportColumns.All.ToList();
        if (mode == "missing") headers.Remove(InitialReceivableImportColumns.Balance);
        if (mode == "duplicate") headers.Add(InitialReceivableImportColumns.DocumentNumber);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("CxC Inicial");
        for (var i = 0; i < headers.Count; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var act = () => _reader.ReadAsync(stream, default);

        await act.Should().ThrowAsync<DomainRuleViolationException>().WithMessage(
            mode == "missing" ? "*Faltan encabezados*Saldo Pendiente*" : "*Encabezado duplicado*");
    }

    [Fact]
    public async Task Archivo_que_no_es_Excel_se_rechaza_con_mensaje_publico()
    {
        var act = () => _reader.ReadAsync(new MemoryStream([1, 2, 3]), default);

        await act.Should().ThrowAsync<DomainRuleViolationException>().WithMessage("*no es un Excel*");
    }
}
