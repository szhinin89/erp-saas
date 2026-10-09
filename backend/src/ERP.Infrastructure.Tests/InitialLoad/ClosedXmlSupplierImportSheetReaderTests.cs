using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.Exceptions;
using ERP.Infrastructure.InitialLoad;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>IL-3A — contrato explícito de la plantilla de Proveedores.</summary>
public sealed class ClosedXmlSupplierImportSheetReaderTests
{
    private readonly ClosedXmlSupplierImportSheetReader _reader = new();

    [Fact]
    public async Task Plantilla_roundtrip_contrato_explicito_y_texto()
    {
        var bytes = await _reader.BuildTemplateAsync(default);
        using var stream = new MemoryStream(bytes);

        var result = await _reader.ReadAsync(stream, default);

        result.Rows.Should().ContainSingle();
        var row = result.Rows[0];
        row.Keys.Should().Equal(SupplierImportColumns.All);
        row[SupplierImportColumns.IdentificationType].Should().Be("04");
        row[SupplierImportColumns.PaymentTermCode].Should().Be("CONTADO");
        row[SupplierImportColumns.IsRequiredToKeepAccounting].Should().Be("SI");
        row[SupplierImportColumns.IsRetentionExempt].Should().Be("NO");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var data = workbook.Worksheet("Proveedores");
        data.Row(2).LastCellUsed()!.Address.ColumnNumber.Should()
            .BeLessThanOrEqualTo(SupplierImportColumns.All.Count, "el ejemplo no deja valores sin encabezado");
        foreach (var column in new[] { SupplierImportColumns.IdentificationType,
                     SupplierImportColumns.IdentificationNumber, SupplierImportColumns.Phone })
        {
            var index = SupplierImportColumns.All.ToList().IndexOf(column) + 1;
            data.Column(index).Style.NumberFormat.Format.Should().Be("@", column + " debe ser Texto");
        }
        var instructions = string.Join(" ", workbook.Worksheet("Instrucciones").CellsUsed().Select(c => c.GetString()));
        instructions.Should().Contain("04 = RUC, 08 = Exterior").And.NotContain("05 = Cédula")
            .And.Contain("No hay valor por defecto").And.Contain("el lote no se confirma");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public async Task Encabezados_invalidos_se_rechazan(string mode)
    {
        var headers = SupplierImportColumns.All.ToList();
        if (mode == "missing") headers.Remove(SupplierImportColumns.IsRetentionExempt);
        if (mode == "duplicate") headers.Add(SupplierImportColumns.Email);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Proveedores");
        for (var i = 0; i < headers.Count; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Cell(2, 1).Value = "04";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var act = () => _reader.ReadAsync(stream, default);

        await act.Should().ThrowAsync<DomainRuleViolationException>()
            .WithMessage(mode == "missing" ? "*Faltan encabezados*Exento de retención*" : "*Encabezado duplicado*");
    }
}
