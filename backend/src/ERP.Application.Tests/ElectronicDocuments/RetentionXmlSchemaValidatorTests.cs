using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Infrastructure.Services.ElectronicDocuments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Xml.Schema;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// RETENTIONS-SRI-SCHEMA-VALIDATOR-04C — mismo patrón exacto que
/// <c>CreditNoteXmlSchemaValidatorTests</c>/<c>InvoiceXmlSchemaValidatorTests</c> para los casos
/// unitarios (fake <see cref="IXmlSchemaProvider"/>), más una validación de extremo a extremo
/// contra el XSD oficial embebido real (mismo criterio que <c>RetentionXmlBuilderTests</c>,
/// 03B) usando XML producido por <see cref="RetentionXmlBuilder"/> — la misma clase de
/// producción que emite el XML real.
/// </summary>
public sealed class RetentionXmlSchemaValidatorTests
{
    private sealed class FakeSchemaProvider : IXmlSchemaProvider
    {
        private readonly XmlSchemaSet? _schemaSet;

        public FakeSchemaProvider(XmlSchemaSet? schemaSet) => _schemaSet = schemaSet;

        public Task<XmlSchemaSet?> GetSchemaSetAsync(
            ElectronicDocumentType documentType,
            string schemaVersion,
            CancellationToken ct = default
        ) => Task.FromResult(_schemaSet);
    }

    private static ElectronicDocumentXml SampleXml(string xml) =>
        new(
            Xml: xml,
            Encoding: "UTF-8",
            Version: "1.0.0",
            DocumentType: ElectronicDocumentType.Retention,
            AccessKey: new string('1', 49),
            GeneratedAtUtc: DateTime.UtcNow
        );

    [Fact]
    public async Task ValidateAsync_when_schema_not_available_returns_invalid_without_throwing()
    {
        var validator = new RetentionXmlSchemaValidator(new FakeSchemaProvider(null));

        var result = await validator.ValidateAsync(SampleXml("<comprobanteRetencion/>"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("no está"));
        result.DocumentType.Should().Be(ElectronicDocumentType.Retention);
        result.SchemaVersion.Should().Be("1.0.0");
    }

    private const string MinimalXsd = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="root">
            <xs:complexType>
              <xs:sequence>
                <xs:element name="requiredField" type="xs:string" minOccurs="1" maxOccurs="1"/>
                <xs:element name="numberField" type="xs:int" minOccurs="1" maxOccurs="1"/>
              </xs:sequence>
            </xs:complexType>
          </xs:element>
        </xs:schema>
        """;

    private static XmlSchemaSet BuildMinimalSchemaSet()
    {
        using var reader = System.Xml.XmlReader.Create(new StringReader(MinimalXsd));
        var schema = XmlSchema.Read(
            reader,
            (_, e) => throw new InvalidOperationException(e.Message)
        )!;
        var set = new XmlSchemaSet();
        set.Add(schema);
        set.Compile();
        return set;
    }

    [Fact]
    public async Task ValidateAsync_valid_xml_returns_no_errors()
    {
        var validator = new RetentionXmlSchemaValidator(
            new FakeSchemaProvider(BuildMinimalSchemaSet())
        );
        var xml = SampleXml(
            "<root><requiredField>abc</requiredField><numberField>42</numberField></root>"
        );

        var result = await validator.ValidateAsync(xml);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_missing_required_node_is_reported()
    {
        var validator = new RetentionXmlSchemaValidator(
            new FakeSchemaProvider(BuildMinimalSchemaSet())
        );
        var xml = SampleXml("<root><numberField>42</numberField></root>");

        var result = await validator.ValidateAsync(xml);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("requiredField"));
    }

    [Fact]
    public async Task ValidateAsync_wrong_type_is_reported()
    {
        var validator = new RetentionXmlSchemaValidator(
            new FakeSchemaProvider(BuildMinimalSchemaSet())
        );
        var xml = SampleXml(
            "<root><requiredField>abc</requiredField><numberField>not-a-number</numberField></root>"
        );

        var result = await validator.ValidateAsync(xml);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("numberField"));
    }

    [Fact]
    public async Task ValidateAsync_malformed_xml_is_reported_as_error_not_exception()
    {
        var validator = new RetentionXmlSchemaValidator(
            new FakeSchemaProvider(BuildMinimalSchemaSet())
        );
        var xml = SampleXml("<root><requiredField>abc</requiredField>");

        var result = await validator.ValidateAsync(xml);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    // ── Contra el XSD oficial real (ComprobanteRetencion_V1.0.0.xsd) ─────────

    private static RetentionElectronicDocumentData ValidRetentionData() =>
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
                TradeName: null,
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
                AuthorizationNumber: null,
                IssueDate: new DateOnly(2026, 8, 1),
                Subtotal: 100m,
                Total: 115m
            ),
            Lines:
            [
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
                TotalRetainedVat: 0m,
                TotalRetainedIncome: 8m,
                TotalRetained: 8m
            ),
            AdditionalInfo: []
        );

    private static string BuildRealRetentionXml()
    {
        var result = new RetentionXmlBuilder().Build(ValidRetentionData());
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!.Xml;
    }

    private static RetentionXmlSchemaValidator RealValidator() =>
        new(new EmbeddedXmlSchemaProvider(NullLogger<EmbeddedXmlSchemaProvider>.Instance));

    [Fact]
    public async Task ValidateAsync_accepts_the_real_xml_produced_by_RetentionXmlBuilder()
    {
        var xml = BuildRealRetentionXml();

        var result = await RealValidator().ValidateAsync(SampleXml(xml));

        result.IsValid.Should().BeTrue(string.Join(" ", result.Errors));
        result.Errors.Should().BeEmpty();
        result.SchemaVersion.Should().Be("1.0.0");
        result.DocumentType.Should().Be(ElectronicDocumentType.Retention);
    }

    [Fact]
    public async Task ValidateAsync_rejects_a_structurally_invalid_retention_xml()
    {
        // Estructura real de comprobanteRetencion pero sin infoCompRetencion ni impuestos —
        // ambos obligatorios en el XSD 1.0.0 (ver ComprobanteRetencion_V1.0.0.xsd).
        const string invalidXml =
            "<comprobanteRetencion id=\"comprobante\" version=\"1.0.0\"><infoTributaria/></comprobanteRetencion>";

        var result = await RealValidator().ValidateAsync(SampleXml(invalidXml));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_uses_schema_version_1_0_0_not_2_0_0()
    {
        var xml = BuildRealRetentionXml();

        var result = await RealValidator().ValidateAsync(SampleXml(xml));

        result.SchemaVersion.Should().Be("1.0.0");
        result.SchemaVersion.Should().NotBe("2.0.0");
    }
}
