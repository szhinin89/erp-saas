using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using FluentAssertions;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

public sealed class PurchaseXmlPaymentTermsTests
{
    private static PurchaseDraftDto Draft => new(null, "", "", "01", "001-001-000005545",
        new DateOnly(2026, 10, 19), null, null, null, null, [], "PROCESSED", null);

    [Theory]
    [InlineData("8", "dias", "2026-10-27")]
    [InlineData("8.00", "DÍAS", "2026-10-27")]
    [InlineData("0", "dias", "2026-10-19")]
    [InlineData("8", "meses", null)]
    [InlineData("8.5", "dias", null)]
    [InlineData("-8", "dias", null)]
    [InlineData("999999999", "dias", null)]
    [InlineData("", "dias", null)]
    public void Only_unambiguous_whole_calendar_days_propose_a_date(string term, string unit, string? expected)
    {
        var result = PurchaseXmlPaymentTerms.Apply(Draft,
            $"<factura><infoFactura><pagos><pago><plazo>{term}</plazo><unidadTiempo>{unit}</unidadTiempo></pago></pagos></infoFactura></factura>");
        result.SuggestedDueDate.Should().Be(expected is null ? null : DateOnly.ParseExact(expected, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        result.XmlPaymentTerm.Should().Be(term);
        result.XmlPaymentTimeUnit.Should().Be(unit);
    }

    [Theory]
    [InlineData("<factura><infoFactura/></factura>", 0)]
    [InlineData("<factura><infoFactura><pagos><pago/></pagos></infoFactura></factura>", 1)]
    [InlineData("<factura><infoFactura><pagos><pago><plazo>8</plazo><unidadTiempo>dias</unidadTiempo></pago><pago><plazo>30</plazo><unidadTiempo>dias</unidadTiempo></pago></pagos></infoFactura></factura>", 2)]
    [InlineData("not xml", 0)]
    public void Missing_or_multiple_payments_never_invent_a_date(string xml, int count)
    {
        var result = PurchaseXmlPaymentTerms.Apply(Draft, xml);
        result.SuggestedDueDate.Should().BeNull();
        result.XmlPaymentCount.Should().Be(count);
    }
}
