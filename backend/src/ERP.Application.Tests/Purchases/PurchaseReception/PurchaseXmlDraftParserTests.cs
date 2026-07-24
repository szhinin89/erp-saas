using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using FluentAssertions;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

/// <summary>
/// Prueba <see cref="PurchaseXmlDraftParser"/> contra XML real, no artesanal: los fixtures se
/// generan con <c>InvoiceXmlBuilder</c> (ElectronicDocuments, FROZEN) — la misma clase de
/// producción que emite el XML que un proveedor efectivamente autoriza — para garantizar que la
/// forma exacta coincide con lo que el SRI realmente entrega.
/// </summary>
public sealed class PurchaseXmlDraftParserTests
{
    private sealed class FakeTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
    {
        public string? Resolve(string taxCode) => taxCode switch
        {
            "VAT" => "2",
            "ICE" => "3",
            _ => null,
        };
    }

    private static string BuildAuthorizedXml(ElectronicDocumentData data)
    {
        var builder = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver());
        var result = builder.Build(data);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!.Xml;
    }

    [Fact]
    public void Parses_header_and_a_single_line_with_vat_only()
    {
        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                Environment: "1", EmissionType: "1", DocTypeCode: "01",
                Establishment: "001", EstablishmentAddress: "Av. Amazonas y Naciones Unidas",
                EmissionPoint: "001", Sequential: "000000123", IssueDate: new DateTime(2026, 7, 8)),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001", LegalName: "PROVEEDOR ACME S.A.", TradeName: "ACME",
                MatrixAddress: "Av. Amazonas y Naciones Unidas", TaxRegime: null, IsAccountingRequired: true),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: "05", IdentificationNumber: "1710034065",
                LegalName: "Empresa Compradora", Address: "Calle Falsa 123", Email: null),
            Details: [new ElectronicDocumentDetailLine(
                Code: "SKU-001", Description: "Producto de prueba", Quantity: 2m, UnitPrice: 10m,
                Discount: 0m, Subtotal: 20m,
                Taxes: [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)])],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 20m, 3m)],
            Totals: new ElectronicDocumentTotals(20m, 0m, 3m, 23m, "USD"),
            Payments: [new ElectronicDocumentPayment("01", 23m, null, null)],
            AdditionalInfo: []);

        var xml = BuildAuthorizedXml(data);
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var draft = result.Value!;

        draft.SupplierRuc.Should().Be("1790012345001");
        draft.SupplierName.Should().Be("PROVEEDOR ACME S.A.");
        draft.DocTypeCode.Should().Be("01");
        draft.InvoiceNumber.Should().Be("001-001-000000123");
        draft.IssueDate.Should().Be(new DateOnly(2026, 7, 8));
        draft.SriPaymentMethodCode.Should().Be("01");

        draft.Lines.Should().ContainSingle();
        var line = draft.Lines[0];
        line.Description.Should().Be("Producto de prueba");
        line.Quantity.Should().Be(2m);
        line.UnitPrice.Should().Be(10m);
        line.DiscountPct.Should().Be(0m);
        line.VatCode.Should().Be("2");
        line.IceCode.Should().BeNull();

        line.SupplierCode.Should().Be("SKU-001");
        line.SupplierAuxCode.Should().BeNull();
        line.Discount.Should().Be(0m);
        line.LineSubtotal.Should().Be(20m);
        line.TaxCode.Should().Be("2");
        line.VatPercentage.Should().Be(15m);
        line.TaxValue.Should().Be(3m);
        line.TotalLine.Should().Be(23m);
    }

    [Fact]
    public void Parses_a_line_with_ice_and_a_discount_percentage()
    {
        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                Environment: "2", EmissionType: "1", DocTypeCode: "01",
                Establishment: "001", EstablishmentAddress: "Av. 6 de Diciembre",
                EmissionPoint: "002", Sequential: "000000456", IssueDate: new DateTime(2026, 7, 10)),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001", LegalName: "PROVEEDOR ACME S.A.", TradeName: null,
                MatrixAddress: "Av. Amazonas y Naciones Unidas", TaxRegime: "CONTRIBUYENTE RÉGIMEN RIMPE",
                IsAccountingRequired: false),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: "04", IdentificationNumber: "1790012345001",
                LegalName: "Cliente Corporativo S.A.", Address: null, Email: null),
            Details:
            [
                new ElectronicDocumentDetailLine(
                    Code: "SKU-002", Description: "Producto con ICE", Quantity: 1m, UnitPrice: 50m,
                    Discount: 5m, Subtotal: 45m,
                    Taxes:
                    [
                        new ElectronicDocumentDetailTax("VAT", "2", 45m, 15m, 6.75m),
                        new ElectronicDocumentDetailTax("ICE", "3010", 50m, 10m, 5m),
                    ]),
            ],
            TaxSummary:
            [
                new ElectronicDocumentTaxSummary("VAT", "2", 45m, 6.75m),
                new ElectronicDocumentTaxSummary("ICE", "3010", 50m, 5m),
            ],
            Totals: new ElectronicDocumentTotals(45m, 5m, 11.75m, 56.75m, "USD"),
            Payments: [new ElectronicDocumentPayment("20", 56.75m, null, null)],
            AdditionalInfo: []);

        var xml = BuildAuthorizedXml(data);
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.Lines.Should().ContainSingle().Subject;

        line.Quantity.Should().Be(1m);
        line.UnitPrice.Should().Be(50m);
        line.DiscountPct.Should().Be(10m);
        line.VatCode.Should().Be("2");
        line.IceCode.Should().Be("3010");

        line.SupplierCode.Should().Be("SKU-002");
        line.Discount.Should().Be(5m);
        line.LineSubtotal.Should().Be(45m);
        line.VatPercentage.Should().Be(15m);
        line.TaxValue.Should().Be(6.75m);
        line.TotalLine.Should().Be(56.75m);
    }

    [Fact]
    public void Fails_gracefully_when_the_xml_is_not_a_valid_factura()
    {
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse("<factura><infoTributaria/></factura>");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }
}
