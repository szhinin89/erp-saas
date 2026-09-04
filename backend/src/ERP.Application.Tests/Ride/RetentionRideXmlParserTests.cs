using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Ride.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Ride;

/// <summary>
/// RETENTIONS-RIDE-TEMPLATE-03C — prueba <see cref="RetentionRideXmlParser"/> contra XML real, no
/// artesanal: el fixture se genera con <see cref="RetentionXmlBuilder"/> (ElectronicDocuments,
/// RETENTIONS-SRI-XML-MAPPER-03B) — la misma clase de producción que emite el XML real — mismo
/// criterio que <c>InvoiceRideXmlParserTests</c>/<c>CreditNoteRideXmlParserTests</c>.
/// </summary>
public sealed class RetentionRideXmlParserTests
{
    private static RetentionElectronicDocumentData ValidData() =>
        new(
            Metadata: new RetentionElectronicDocumentMetadata(
                RetentionId: Guid.NewGuid(),
                TenantId: Guid.NewGuid(),
                CompanyId: Guid.NewGuid(),
                EmissionPointId: Guid.NewGuid(),
                SourceDocumentType: RetentionSourceDocumentType.ExpenseDocument,
                SourceDocumentId: Guid.NewGuid(),
                GeneratedAtUtc: new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc)
            ),
            Emission: new ElectronicDocumentEmissionContext(
                Environment: "1",
                EmissionType: "1",
                DocTypeCode: "07",
                Establishment: "001",
                EstablishmentAddress: "Av. Principal 123",
                EmissionPoint: "001",
                Sequential: "000000001",
                IssueDate: new DateTime(2026, 8, 5)
            ),
            NumeroCompleto: "001-001-000000001",
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001",
                LegalName: "Empresa Test S.A.",
                TradeName: "Empresa Test",
                MatrixAddress: "Matriz 456",
                TaxRegime: null,
                IsAccountingRequired: true
            ),
            RetentionInfo: new RetentionElectronicDocumentInfo(
                SpecialTaxpayerNumber: null,
                FiscalPeriod: "08/2026"
            ),
            SubjectWithheld: new ElectronicDocumentCounterpartyData(
                IdentificationType: "05",
                IdentificationNumber: "1710034065",
                LegalName: "Proveedor Test",
                Address: null,
                Email: null
            ),
            SourceDocument: new RetentionElectronicDocumentSourceDocument(
                TaxSupportCode: "01",
                DocTypeCode: "01",
                Number: "001-001-000000456",
                AuthorizationNumber: "1234567890",
                IssueDate: new DateOnly(2026, 8, 1),
                Subtotal: 100m,
                Total: 115m
            ),
            Lines:
            [
                new RetentionElectronicDocumentTaxLine(
                    TaxType: RetentionTaxType.Vat,
                    SriTaxTypeCode: "2",
                    RetentionCode: "725",
                    RetentionCodeDescription: "IVA retenido 30% bienes",
                    BaseAmount: 15m,
                    RetentionRate: 30m,
                    RetainedAmount: 4.5m
                ),
                new RetentionElectronicDocumentTaxLine(
                    TaxType: RetentionTaxType.Income,
                    SriTaxTypeCode: "1",
                    RetentionCode: "303",
                    RetentionCodeDescription: "Honorarios profesionales",
                    BaseAmount: 100m,
                    RetentionRate: 8m,
                    RetainedAmount: 8m
                ),
            ],
            Totals: new RetentionElectronicDocumentTotals(
                TotalRetainedVat: 4.5m,
                TotalRetainedIncome: 8m,
                TotalRetained: 12.5m
            ),
            AdditionalInfo: []
        );

    private static string BuildAuthorizedXml(RetentionElectronicDocumentData data)
    {
        var result = new RetentionXmlBuilder().Build(data);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!.Xml;
    }

    [Fact]
    public void Parses_the_full_number_establishment_emissionPoint_sequential()
    {
        var xml = BuildAuthorizedXml(ValidData());
        var parser = new RetentionRideXmlParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var model = result.Value!;
        model.Header.Establishment.Should().Be("001");
        model.Header.EmissionPoint.Should().Be("001");
        model.Header.Sequential.Should().Be("000000001");
    }

    [Fact]
    public void Parses_the_access_key_and_derives_the_authorization_number_from_it()
    {
        var xml = BuildAuthorizedXml(ValidData());
        var parser = new RetentionRideXmlParser();

        var result = parser.Parse(xml);

        var model = result.Value!;
        model.Header.AccessKey.Value.Should().HaveLength(49);
        model.Header.AuthorizationNumber.Should().Be(model.Header.AccessKey.Value);
        model.Header.AuthorizationDate.Should().BeNull();
    }

    [Fact]
    public void Parses_the_issuer_data()
    {
        var xml = BuildAuthorizedXml(ValidData());
        var parser = new RetentionRideXmlParser();

        var model = parser.Parse(xml).Value!;

        model.Issuer.IdentificationNumber.Should().Be("1790012345001");
        model.Issuer.LegalName.Should().Be("Empresa Test S.A.");
        model.Issuer.TradeName.Should().Be("Empresa Test");
        model.Issuer.Address.Should().Be("Matriz 456");
        model.Issuer.IsAccountingRequired.Should().BeTrue();
        model.Header.EstablishmentAddress.Should().Be("Av. Principal 123");
    }

    [Fact]
    public void Parses_the_subject_withheld()
    {
        var xml = BuildAuthorizedXml(ValidData());
        var parser = new RetentionRideXmlParser();

        var model = parser.Parse(xml).Value!;

        model.SubjectWithheld.IdentificationType.Should().Be("05");
        model.SubjectWithheld.IdentificationNumber.Should().Be("1710034065");
        model.SubjectWithheld.LegalName.Should().Be("Proveedor Test");
    }

    [Fact]
    public void Parses_the_fiscal_period()
    {
        var xml = BuildAuthorizedXml(ValidData());
        var parser = new RetentionRideXmlParser();

        var model = parser.Parse(xml).Value!;

        model.Header.FiscalPeriod.Should().Be("08/2026");
    }

    [Fact]
    public void Parses_the_source_document_from_the_first_tax_line()
    {
        var xml = BuildAuthorizedXml(ValidData());
        var parser = new RetentionRideXmlParser();

        var model = parser.Parse(xml).Value!;

        model.SourceDocument.DocumentTypeCode.Should().Be("01");
        // "001-001-000000456" sin guiones = 15 dígitos, como los escribe RetentionXmlBuilder.
        model.SourceDocument.Number.Should().Be("001001000000456");
        model.SourceDocument.IssueDate.Should().Be(new DateOnly(2026, 8, 1));
    }

    [Fact]
    public void Parses_the_vat_and_income_lines_without_recalculating_anything()
    {
        var xml = BuildAuthorizedXml(ValidData());
        var parser = new RetentionRideXmlParser();

        var model = parser.Parse(xml).Value!;

        model.Lines.Should().HaveCount(2);
        model
            .Lines.Should()
            .Contain(l =>
                l.TaxCode == "2"
                && l.RetentionCode == "725"
                && l.BaseAmount == 15m
                && l.RetentionRate == 30m
                && l.RetainedAmount == 4.5m
            );
        model
            .Lines.Should()
            .Contain(l =>
                l.TaxCode == "1"
                && l.RetentionCode == "303"
                && l.BaseAmount == 100m
                && l.RetentionRate == 8m
                && l.RetainedAmount == 8m
            );
    }

    [Fact]
    public void Computes_the_visual_total_from_the_lines_in_the_xml_not_from_the_domain()
    {
        var xml = BuildAuthorizedXml(ValidData());
        var parser = new RetentionRideXmlParser();

        var model = parser.Parse(xml).Value!;

        // 4.50 + 8.00, tal como aparecen en el propio XML — nunca una consulta a RetentionDocument.
        model.TotalRetained.Should().Be(12.5m);
    }

    [Fact]
    public void Omits_source_document_number_when_it_does_not_have_15_digits()
    {
        var data = ValidData() with
        {
            SourceDocument = ValidData().SourceDocument with { Number = "ABC-123" },
        };
        var xml = BuildAuthorizedXml(data);
        var parser = new RetentionRideXmlParser();

        var model = parser.Parse(xml).Value!;

        model.SourceDocument.Number.Should().BeNull();
    }

    [Fact]
    public void DocumentType_is_Retention()
    {
        new RetentionRideXmlParser().DocumentType.Should().Be(RideDocumentType.Retention);
    }
}
