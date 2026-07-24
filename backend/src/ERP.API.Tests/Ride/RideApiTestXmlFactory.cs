using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using FluentAssertions;

namespace ERP.API.Tests.Ride;

/// <summary>Genera un XML de factura real vía <c>InvoiceXmlBuilder</c> (ElectronicDocuments, FROZEN) — mismo criterio que el resto de la suite de Ride.</summary>
internal static class RideApiTestXmlFactory
{
    private sealed class FakeTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
    {
        public string? Resolve(string taxCode) => taxCode switch { "VAT" => "2", _ => null };
    }

    public static string RealAuthorizedInvoiceXml()
    {
        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                "1", "1", "01", "001", "Av. Amazonas y Naciones Unidas", "001", "000000123", new DateTime(2026, 7, 8)),
            Issuer: new ElectronicDocumentIssuerData(
                "1790012345001", "ACME CIA LTDA", "ACME", "Av. Amazonas y Naciones Unidas", null, true),
            Counterparty: new ElectronicDocumentCounterpartyData(
                "05", "1710034065", "Juan Pérez", "Calle Falsa 123", "juan@example.com"),
            Details: [new ElectronicDocumentDetailLine(
                "SKU-001", "Producto de prueba", 2m, 10m, 0m, 20m,
                [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)])],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 20m, 3m)],
            Totals: new ElectronicDocumentTotals(20m, 0m, 3m, 23m, "USD"),
            Payments: [new ElectronicDocumentPayment("01", 23m, null, null)],
            AdditionalInfo: []);

        var result = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver()).Build(data);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!.Xml;
    }
}
