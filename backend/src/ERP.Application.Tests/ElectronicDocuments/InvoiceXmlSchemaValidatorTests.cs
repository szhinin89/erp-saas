using System.Xml.Schema;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.ElectronicDocuments;

public sealed class InvoiceXmlSchemaValidatorTests
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
            DocumentType: ElectronicDocumentType.Invoice,
            AccessKey: new string('1', 49),
            GeneratedAtUtc: DateTime.UtcNow
        );

    [Fact]
    public async Task ValidateAsync_when_schema_not_available_returns_invalid_without_throwing()
    {
        var validator = new InvoiceXmlSchemaValidator(new FakeSchemaProvider(null));

        var result = await validator.ValidateAsync(SampleXml("<factura/>"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("no está"));
        result.DocumentType.Should().Be(ElectronicDocumentType.Invoice);
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
                <xs:element name="shortField" minOccurs="0" maxOccurs="1">
                  <xs:simpleType>
                    <xs:restriction base="xs:string">
                      <xs:maxLength value="3"/>
                    </xs:restriction>
                  </xs:simpleType>
                </xs:element>
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
        var validator = new InvoiceXmlSchemaValidator(
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
        var validator = new InvoiceXmlSchemaValidator(
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
        var validator = new InvoiceXmlSchemaValidator(
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
    public async Task ValidateAsync_length_violation_is_reported()
    {
        var validator = new InvoiceXmlSchemaValidator(
            new FakeSchemaProvider(BuildMinimalSchemaSet())
        );
        var xml = SampleXml(
            "<root><requiredField>abc</requiredField><numberField>1</numberField><shortField>TOO-LONG</shortField></root>"
        );

        var result = await validator.ValidateAsync(xml);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("shortField"));
    }

    [Fact]
    public async Task ValidateAsync_malformed_xml_is_reported_as_error_not_exception()
    {
        var validator = new InvoiceXmlSchemaValidator(
            new FakeSchemaProvider(BuildMinimalSchemaSet())
        );
        var xml = SampleXml("<root><requiredField>abc</requiredField>");

        var result = await validator.ValidateAsync(xml);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_accumulates_multiple_errors_in_a_single_pass()
    {
        var validator = new InvoiceXmlSchemaValidator(
            new FakeSchemaProvider(BuildMinimalSchemaSet())
        );
        // numberField con tipo incorrecto Y shortField que excede la longitud máxima:
        // dos violaciones de contenido independientes, ambas en la misma pasada.
        var xml = SampleXml(
            "<root><requiredField>abc</requiredField><numberField>not-a-number</numberField><shortField>TOO-LONG</shortField></root>"
        );

        var result = await validator.ValidateAsync(xml);

        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().BeGreaterThan(1);
        result.Errors.Should().Contain(e => e.Contains("numberField"));
        result.Errors.Should().Contain(e => e.Contains("shortField"));
    }
}
