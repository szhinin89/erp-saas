using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Domain.Modules.Ride.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Ride;

/// <summary>
/// Prueba <see cref="InvoiceRideXmlParser"/> contra XML real, no artesanal: los 3 fixtures se
/// generan con <c>InvoiceXmlBuilder</c> (ElectronicDocuments, FROZEN) — la misma clase de
/// producción que emite el XML real — para garantizar que la forma exacta coincide con lo que
/// ElectronicDocuments realmente produce.
/// </summary>
public sealed class InvoiceRideXmlParserTests
{
    private sealed class FakeTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
    {
        public string? Resolve(string taxCode) =>
            taxCode switch
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
    public void Standard_invoice_populates_the_full_RideModel()
    {
        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                Environment: "1",
                EmissionType: "1",
                DocTypeCode: "01",
                Establishment: "001",
                EstablishmentAddress: "Av. Amazonas y Naciones Unidas",
                EmissionPoint: "001",
                Sequential: "000000123",
                IssueDate: new DateTime(2026, 7, 8)
            ),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001",
                LegalName: "ACME CIA LTDA",
                TradeName: "ACME",
                MatrixAddress: "Av. Amazonas y Naciones Unidas",
                TaxRegime: null,
                IsAccountingRequired: true
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: "05",
                IdentificationNumber: "1710034065",
                LegalName: "Juan Pérez",
                Address: "Calle Falsa 123",
                Email: "juan@example.com"
            ),
            Details:
            [
                new ElectronicDocumentDetailLine(
                    Code: "SKU-001",
                    Description: "Producto de prueba",
                    Quantity: 2m,
                    UnitPrice: 10m,
                    Discount: 0m,
                    Subtotal: 20m,
                    Taxes: [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)]
                ),
            ],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 20m, 3m)],
            Totals: new ElectronicDocumentTotals(20m, 0m, 3m, 23m, "USD"),
            Payments: [new ElectronicDocumentPayment("01", 23m, null, null)],
            AdditionalInfo: []
        );

        var xml = BuildAuthorizedXml(data);
        var parser = new InvoiceRideXmlParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var model = result.Value!;

        model.Header.Environment.Should().Be("1");
        model.Header.EmissionType.Should().Be("1");
        model.Header.DocumentTypeCode.Should().Be("01");
        model.Header.Establishment.Should().Be("001");
        model.Header.EmissionPoint.Should().Be("001");
        model.Header.Sequential.Should().Be("000000123");
        model.Header.EstablishmentAddress.Should().Be("Av. Amazonas y Naciones Unidas");
        model.Header.IssueDate.Should().Be(new DateOnly(2026, 7, 8));
        model.Header.CurrencyCode.Should().Be("USD");
        model.Header.AccessKey.Value.Should().HaveLength(49);
        model.Header.AuthorizationNumber.Should().Be(model.Header.AccessKey.Value);
        model.Header.AuthorizationDate.Should().BeNull();
        model.Header.SubtotalWithoutTax.Should().Be(20m);
        model.Header.TotalDiscount.Should().Be(0m);
        model.Header.Tip.Should().Be(0m);
        model.Header.GrandTotal.Should().Be(23m);

        model.Issuer.IdentificationNumber.Should().Be("1790012345001");
        model.Issuer.LegalName.Should().Be("ACME CIA LTDA");
        model.Issuer.TradeName.Should().Be("ACME");
        model.Issuer.Address.Should().Be("Av. Amazonas y Naciones Unidas");
        model.Issuer.IsAccountingRequired.Should().BeTrue();
        model.Issuer.TaxRegime.Should().BeNull();

        model.Receiver.IdentificationType.Should().Be("05");
        model.Receiver.IdentificationNumber.Should().Be("1710034065");
        model.Receiver.LegalName.Should().Be("Juan Pérez");
        model.Receiver.Address.Should().Be("Calle Falsa 123");

        model.Lines.Should().HaveCount(1);
        var line = model.Lines[0];
        line.Code.Should().Be("SKU-001");
        line.Description.Should().Be("Producto de prueba");
        line.Quantity.Should().Be(2m);
        line.UnitPrice.Should().Be(10m);
        line.Discount.Should().Be(0m);
        line.Subtotal.Should().Be(20m);
        line.Taxes.Should().ContainSingle();
        line.Taxes[0].TaxCode.Should().Be("2");
        line.Taxes[0].TaxPercentageCode.Should().Be("2");
        line.Taxes[0].Rate.Should().Be(15m);
        line.Taxes[0].TaxableBase.Should().Be(20m);
        line.Taxes[0].TaxAmount.Should().Be(3m);

        model.TaxSummary.Should().ContainSingle();
        model.TaxSummary[0].TaxCode.Should().Be("2");
        model.TaxSummary[0].TaxableBase.Should().Be(20m);
        model.TaxSummary[0].TaxAmount.Should().Be(3m);
        model.TaxSummary[0].Rate.Should().BeNull();

        model.Payments.Should().ContainSingle();
        model.Payments[0].PaymentMethodCode.Should().Be("01");
        model.Payments[0].Amount.Should().Be(23m);
        model.Payments[0].Term.Should().BeNull();
        model.Payments[0].TimeUnit.Should().BeNull();

        model.AdditionalInfo.Should().BeEmpty();
    }

    [Fact]
    public void Invoice_with_vat_and_ice_across_two_lines_maps_every_tax_correctly()
    {
        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                Environment: "2",
                EmissionType: "1",
                DocTypeCode: "01",
                Establishment: "001",
                EstablishmentAddress: "Av. 6 de Diciembre",
                EmissionPoint: "002",
                Sequential: "000000456",
                IssueDate: new DateTime(2026, 7, 10)
            ),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001",
                LegalName: "ACME CIA LTDA",
                TradeName: null,
                MatrixAddress: "Av. Amazonas y Naciones Unidas",
                TaxRegime: "CONTRIBUYENTE RÉGIMEN RIMPE",
                IsAccountingRequired: false
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: "04",
                IdentificationNumber: "1790012345001",
                LegalName: "Cliente Corporativo S.A.",
                Address: null,
                Email: null
            ),
            Details:
            [
                new ElectronicDocumentDetailLine(
                    "SKU-001",
                    "Producto sin ICE",
                    2m,
                    10m,
                    0m,
                    20m,
                    [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)]
                ),
                new ElectronicDocumentDetailLine(
                    "SKU-002",
                    "Producto con ICE",
                    1m,
                    50m,
                    0m,
                    50m,
                    [
                        new ElectronicDocumentDetailTax("VAT", "2", 50m, 15m, 7.5m),
                        new ElectronicDocumentDetailTax("ICE", "3010", 50m, 10m, 5m),
                    ]
                ),
            ],
            TaxSummary:
            [
                new ElectronicDocumentTaxSummary("VAT", "2", 70m, 10.5m),
                new ElectronicDocumentTaxSummary("ICE", "3010", 50m, 5m),
            ],
            Totals: new ElectronicDocumentTotals(70m, 0m, 15.5m, 85.5m, "USD"),
            Payments: [new ElectronicDocumentPayment("01", 85.5m, null, null)],
            AdditionalInfo: []
        );

        var xml = BuildAuthorizedXml(data);
        var parser = new InvoiceRideXmlParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var model = result.Value!;

        model.Lines.Should().HaveCount(2);
        model.Lines[0].Taxes.Should().ContainSingle(t => t.TaxCode == "2");
        model.Lines[1].Taxes.Should().HaveCount(2);
        model.Lines[1].Taxes.Should().Contain(t => t.TaxCode == "2" && t.TaxAmount == 7.5m);
        model
            .Lines[1]
            .Taxes.Should()
            .Contain(t => t.TaxCode == "3" && t.TaxAmount == 5m && t.TaxPercentageCode == "3010");

        model.TaxSummary.Should().HaveCount(2);
        model
            .TaxSummary.Should()
            .Contain(t => t.TaxCode == "2" && t.TaxableBase == 70m && t.TaxAmount == 10.5m);
        model
            .TaxSummary.Should()
            .Contain(t => t.TaxCode == "3" && t.TaxableBase == 50m && t.TaxAmount == 5m);

        model.Header.GrandTotal.Should().Be(85.5m);
        model.Issuer.TaxRegime.Should().Be("CONTRIBUYENTE RÉGIMEN RIMPE");
        model.Issuer.TradeName.Should().BeNull();
        model.Issuer.IsAccountingRequired.Should().BeFalse();
        model.Receiver.Address.Should().BeNull();
    }

    [Fact]
    public void Invoice_with_additional_info_and_multiple_payments_populates_everything()
    {
        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                Environment: "1",
                EmissionType: "1",
                DocTypeCode: "01",
                Establishment: "001",
                EstablishmentAddress: "Av. Amazonas y Naciones Unidas",
                EmissionPoint: "001",
                Sequential: "000000789",
                IssueDate: new DateTime(2026, 7, 11)
            ),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001",
                LegalName: "ACME CIA LTDA",
                TradeName: "ACME",
                MatrixAddress: "Av. Amazonas y Naciones Unidas",
                TaxRegime: null,
                IsAccountingRequired: true
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: "05",
                IdentificationNumber: "1710034065",
                LegalName: "Juan Pérez",
                Address: "Calle Falsa 123",
                Email: "juan@example.com"
            ),
            Details:
            [
                new ElectronicDocumentDetailLine(
                    "SKU-001",
                    "Producto de prueba",
                    10m,
                    10m,
                    0m,
                    100m,
                    [new ElectronicDocumentDetailTax("VAT", "2", 100m, 15m, 15m)]
                ),
            ],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 100m, 15m)],
            Totals: new ElectronicDocumentTotals(100m, 0m, 15m, 115m, "USD"),
            Payments:
            [
                new ElectronicDocumentPayment("01", 50m, null, null),
                new ElectronicDocumentPayment("16", 65m, 30, "dias"),
            ],
            AdditionalInfo:
            [
                new ElectronicDocumentAdditionalField("Email", "cliente@example.com"),
                new ElectronicDocumentAdditionalField("Telefono", "0999999999"),
            ]
        );

        var xml = BuildAuthorizedXml(data);
        var parser = new InvoiceRideXmlParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var model = result.Value!;

        model.AdditionalInfo.Should().HaveCount(2);
        model
            .AdditionalInfo.Should()
            .Contain(f => f.Name == "Email" && f.Value == "cliente@example.com");
        model.AdditionalInfo.Should().Contain(f => f.Name == "Telefono" && f.Value == "0999999999");

        model.Payments.Should().HaveCount(2);
        model
            .Payments.Should()
            .Contain(p => p.PaymentMethodCode == "01" && p.Amount == 50m && p.Term == null);
        model
            .Payments.Should()
            .Contain(p =>
                p.PaymentMethodCode == "16"
                && p.Amount == 65m
                && p.Term == 30
                && p.TimeUnit == "dias"
            );

        model.Header.SubtotalWithoutTax.Should().Be(100m);
        model.Header.GrandTotal.Should().Be(115m);
        model.Lines.Should().ContainSingle();
        model.Lines[0].Quantity.Should().Be(10m);
        model.Lines[0].Subtotal.Should().Be(100m);
    }

    [Fact]
    public void DocumentType_is_Invoice()
    {
        new InvoiceRideXmlParser().DocumentType.Should().Be(RideDocumentType.Invoice);
    }
}
