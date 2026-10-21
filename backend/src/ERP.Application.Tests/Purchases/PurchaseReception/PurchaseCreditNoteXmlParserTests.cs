using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

public sealed class PurchaseCreditNoteXmlParserTests
{
    internal static string MinimalAlpinaXml => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "TestData", "NotaCreditoSinDescuento.xml"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parses_zero_vat_credit_note_with_optional_discount(bool explicitZero)
    {
        var xml = MinimalAlpinaXml;
        if (explicitZero)
            xml = xml.Replace("<precioTotalSinImpuesto>", "<descuento>0</descuento><precioTotalSinImpuesto>");
        var parsed = PurchaseCreditNoteXmlParser.Parse(xml, Guid.NewGuid(), Guid.NewGuid());

        parsed.DocumentNumber.Should().Be("012-100-000449576");
        parsed.ModifiedDocumentNumber.Should().Be("012-004-004230851");
        parsed.Detail.Processing.Status.Should().Be(PurchaseReceptionProcessingStatus.Processed,
            parsed.Detail.Processing.Notes);
        parsed.Detail.Processing.LinesDetected.Should().Be(1);
        parsed.Detail.Processing.LinesProcessed.Should().Be(1);
        var line = parsed.Detail.Lines.Should().ContainSingle().Subject;
        line.SupplierCode.Should().Be("5523");
        line.Description.Should().Be("BY BLCK VASO 165G");
        line.Quantity.Should().Be(1m);
        line.UnitPrice.Should().Be(0.86m);
        line.Discount.Should().Be(0m);
        line.LineSubtotal.Should().Be(0.86m);
        line.TotalLine.Should().Be(0.86m);
        line.VatCode.Should().Be("0");
        line.Taxes.Should().ContainSingle().Which.TaxAmount.Should().Be(0m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public void Rejects_present_but_invalid_discount_without_inventing_a_line(string discount)
    {
        var xml = MinimalAlpinaXml.Replace("<precioTotalSinImpuesto>",
            $"<descuento>{discount}</descuento><precioTotalSinImpuesto>");
        var parsed = PurchaseCreditNoteXmlParser.Parse(xml, Guid.NewGuid(), Guid.NewGuid());
        parsed.Detail.Processing.Status.Should().Be(PurchaseReceptionProcessingStatus.Failed);
        parsed.Detail.Processing.Notes.Should().Contain("Línea 1");
        parsed.Detail.Lines.Should().BeEmpty();
    }
}
