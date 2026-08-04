using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Domain.Modules.Ride.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Ride;

/// <summary>
/// ADR-031 addendum (Fase 12, P0-01) — <see cref="CreditNoteRideXmlParser"/> contra XML real,
/// generado con <c>CreditNoteXmlBuilder</c> (ElectronicDocuments), mismo criterio que
/// <c>RidePipelineInvoiceIntegrationTests</c> usa para Factura: nunca un XML de muestra escrito a
/// mano que pueda divergir del que el sistema realmente produce.
/// </summary>
public sealed class CreditNoteRideXmlParserTests
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

    private static ElectronicDocumentData ValidData(
        bool withIce = false,
        string code = "SKU-001",
        string reason = "Producto en mal estado"
    ) =>
        new(
            Emission: new ElectronicDocumentEmissionContext(
                Environment: "2",
                EmissionType: "1",
                DocTypeCode: "04",
                Establishment: "001",
                EstablishmentAddress: "Av. Principal 123",
                EmissionPoint: "001",
                Sequential: "000000001",
                IssueDate: new DateTime(2026, 7, 30)
            ),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001",
                LegalName: "Empresa Test S.A.",
                TradeName: "Empresa Test",
                MatrixAddress: "Matriz 456",
                TaxRegime: null,
                IsAccountingRequired: true
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: "05",
                IdentificationNumber: "1710034065",
                LegalName: "Cliente Test",
                Address: "Calle Falsa 123",
                Email: null
            ),
            Details:
            [
                new ElectronicDocumentDetailLine(
                    Code: code,
                    Description: "Producto devuelto",
                    Quantity: 2m,
                    UnitPrice: 10m,
                    Discount: 0m,
                    Subtotal: 20m,
                    Taxes: withIce
                        ?
                        [
                            new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m),
                            new ElectronicDocumentDetailTax("ICE", "3010", 20m, 10m, 2m),
                        ]
                        : [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)]
                ),
            ],
            TaxSummary: withIce
                ?
                [
                    new ElectronicDocumentTaxSummary("ICE", "3010", 20m, 2m),
                    new ElectronicDocumentTaxSummary("VAT", "2", 22m, 3.3m),
                ]
                : [new ElectronicDocumentTaxSummary("VAT", "2", 20m, 3m)],
            Totals: new ElectronicDocumentTotals(20m, 0m, withIce ? 2m : 0m, withIce ? 25.3m : 23m, "USD"),
            Payments: [],
            AdditionalInfo: [],
            Reason: reason,
            ModifiedDocument: new ElectronicDocumentModifiedReference(
                DocTypeCode: "01",
                Number: "001-001-000000045",
                IssueDate: new DateTime(2026, 7, 20)
            )
        );

    private static string BuildXml(ElectronicDocumentData data)
    {
        var result = new CreditNoteXmlBuilder(new FakeTaxCategoryCodeResolver()).Build(data);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!.Xml;
    }

    [Fact]
    public void Parse_valid_xml_returns_model_with_reason_and_modified_document()
    {
        var parser = new CreditNoteRideXmlParser();
        parser.DocumentType.Should().Be(RideDocumentType.CreditNote);

        var xml = BuildXml(ValidData());
        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var model = result.Value!;

        model.Header.DocumentTypeCode.Should().Be("04");
        model.Header.GrandTotal.Should().Be(23m);
        model.Header.SubtotalWithoutTax.Should().Be(20m);
        model.Header.TotalDiscount.Should().Be(0m, "NotaCredito V1.1.0 no define descuento total como concepto propio");
        model.Header.Tip.Should().Be(0m, "NotaCredito V1.1.0 no define propina como concepto propio");

        model.Header.Reason.Should().Be("Producto en mal estado");
        model.Header.ModifiedDocument.Should().NotBeNull();
        model.Header.ModifiedDocument!.DocumentTypeCode.Should().Be("01");
        model.Header.ModifiedDocument.Number.Should().Be("001-001-000000045");
        model.Header.ModifiedDocument.IssueDate.Should().Be(new DateOnly(2026, 7, 20));

        model.Issuer.LegalName.Should().Be("Empresa Test S.A.");
        model.Receiver.LegalName.Should().Be("Cliente Test");
        model.Receiver.Address.Should().BeNull("el esquema de NotaCredito no incluye dirección del comprador");
        model.Payments.Should().BeEmpty("el esquema de NotaCredito no tiene sección de pagos");
    }

    [Fact]
    public void Parse_line_without_ice_has_a_single_tax()
    {
        var parser = new CreditNoteRideXmlParser();
        var result = parser.Parse(BuildXml(ValidData(withIce: false)));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines.Should().ContainSingle();
        result.Value.Lines[0].Taxes.Should().ContainSingle(t => t.TaxCode == "2");
    }

    [Fact]
    public void Parse_line_with_ice_has_two_taxes()
    {
        var parser = new CreditNoteRideXmlParser();
        var result = parser.Parse(BuildXml(ValidData(withIce: true)));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines.Should().ContainSingle();
        result.Value.Lines[0].Taxes.Should().HaveCount(2);
        result.Value.Lines[0].Taxes.Should().Contain(t => t.TaxCode == "2" && t.TaxPercentageCode == "2");
        result.Value.Lines[0].Taxes.Should().Contain(t => t.TaxCode == "3" && t.TaxPercentageCode == "3010");
    }

    [Fact]
    public void Parse_line_without_optional_codigoInterno_falls_back_to_placeholder_without_inventing_data()
    {
        // Mismo escenario real que SalesReturnCreditNoteDataProvider produce cuando falta el SKU
        // snapshot: Code queda en string.Empty → CreditNoteXmlBuilder omite <codigoInterno> del
        // XML (es opcional en el esquema, a diferencia de <codigoPrincipal> de Factura).
        var parser = new CreditNoteRideXmlParser();
        var result = parser.Parse(BuildXml(ValidData(code: "")));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines[0].Code.Should().Be("-");
    }

    [Fact]
    public void Parse_reason_at_maximum_schema_length_is_preserved()
    {
        var longReason = new string('A', 300); // maxLength oficial del SRI para <motivo>
        var parser = new CreditNoteRideXmlParser();
        var result = parser.Parse(BuildXml(ValidData(reason: longReason)));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Header.Reason.Should().Be(longReason);
        result.Value.Header.Reason!.Length.Should().Be(300);
    }

    [Fact]
    public void Parse_malformed_xml_returns_validation_failure_not_exception()
    {
        var parser = new CreditNoteRideXmlParser();

        var result = parser.Parse("<notaCredito><infoTributaria></infoTributaria></notaCredito>");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }
}
