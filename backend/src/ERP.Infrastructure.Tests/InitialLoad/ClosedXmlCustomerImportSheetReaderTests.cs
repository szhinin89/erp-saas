using ClosedXML.Excel;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Domain.Exceptions;
using ERP.Infrastructure.InitialLoad;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>IL-2A — contrato explícito de la plantilla de Clientes.</summary>
public sealed class ClosedXmlCustomerImportSheetReaderTests
{
    private readonly ClosedXmlCustomerImportSheetReader _reader = new();

    [Fact]
    public async Task Plantilla_roundtrip_contrato_explicito_y_texto()
    {
        var bytes = await _reader.BuildTemplateAsync(default);
        using var stream = new MemoryStream(bytes);

        var result = await _reader.ReadAsync(stream, default);

        result.Rows.Should().ContainSingle();
        var row = result.Rows[0];
        row.Keys.Should().Equal(CustomerImportColumns.All);
        row[CustomerImportColumns.IdentificationType].Should().Be("04");
        row[CustomerImportColumns.PaymentTermCode].Should().Be("CONTADO");
        row.Keys.Should().NotContain(CustomerImportColumns.Obsolete);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var data = workbook.Worksheet("Clientes");
        foreach (var column in new[] { CustomerImportColumns.IdentificationType,
                     CustomerImportColumns.IdentificationNumber, CustomerImportColumns.Phone })
        {
            var index = CustomerImportColumns.All.ToList().IndexOf(column) + 1;
            data.Column(index).Style.NumberFormat.Format.Should().Be("@", column + " debe ser Texto");
        }
        var instructions = string.Join(" ", workbook.Worksheet("Instrucciones").CellsUsed().Select(c => c.GetString()));
        instructions.Should().Contain("Condición de Pago").And.Contain("No hay valor por defecto")
            .And.Contain("no se duplica").And.Contain("el lote no se confirma");
        instructions.Should().NotContain("Límite de Crédito");
    }

    [Theory]
    [InlineData("obsolete")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public async Task Encabezados_invalidos_se_rechazan(string mode)
    {
        var headers = CustomerImportColumns.All.ToList();
        if (mode == "obsolete") headers.Add("Categoría");
        if (mode == "missing") headers.Remove(CustomerImportColumns.PaymentTermCode);
        if (mode == "duplicate") headers.Add(CustomerImportColumns.Email);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Clientes");
        for (var i = 0; i < headers.Count; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Cell(2, 1).Value = "04";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var act = () => _reader.ReadAsync(stream, default);

        var expected = mode switch
        {
            "obsolete" => "*ya no admite: Categoría*",
            "missing" => "*Faltan encabezados*Condición de Pago*",
            _ => "*Encabezado duplicado*",
        };
        await act.Should().ThrowAsync<DomainRuleViolationException>().WithMessage(expected);
    }
}
