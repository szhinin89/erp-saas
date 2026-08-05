using System.Xml.Schema;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>ADR-031 (Fase 11, P0-01) — mismo patrón exacto que <c>InvoiceXmlSchemaValidatorTests</c>.</summary>
public sealed class CreditNoteXmlSchemaValidatorTests
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
            Version: "1.1.0",
            DocumentType: ElectronicDocumentType.CreditNote,
            AccessKey: new string('1', 49),
            GeneratedAtUtc: DateTime.UtcNow
        );

    [Fact]
    public async Task ValidateAsync_when_schema_not_available_returns_invalid_without_throwing()
    {
        var validator = new CreditNoteXmlSchemaValidator(new FakeSchemaProvider(null));

        var result = await validator.ValidateAsync(SampleXml("<notaCredito/>"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("no está"));
        result.DocumentType.Should().Be(ElectronicDocumentType.CreditNote);
        result.SchemaVersion.Should().Be("1.1.0");
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
        var validator = new CreditNoteXmlSchemaValidator(
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
        var validator = new CreditNoteXmlSchemaValidator(
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
        var validator = new CreditNoteXmlSchemaValidator(
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
        var validator = new CreditNoteXmlSchemaValidator(
            new FakeSchemaProvider(BuildMinimalSchemaSet())
        );
        var xml = SampleXml("<root><requiredField>abc</requiredField>");

        var result = await validator.ValidateAsync(xml);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
}
