using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.SchemaValidation;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;
using System.Xml.Schema;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// RETENTIONS-SRI-SCHEMA-VALIDATOR-04C — confirma que registrar
/// <see cref="RetentionXmlSchemaValidator"/> no requiere ningún cambio en
/// <see cref="ElectronicDocumentSchemaValidatorResolver"/> (contrato genérico por
/// <see cref="ElectronicDocumentType"/> desde su diseño original) y que Invoice/CreditNote
/// siguen resolviendo exactamente igual — regresión explícita.
/// </summary>
public sealed class ElectronicDocumentSchemaValidatorResolverTests
{
    private sealed class NullSchemaProvider : IXmlSchemaProvider
    {
        public Task<XmlSchemaSet?> GetSchemaSetAsync(
            ElectronicDocumentType documentType,
            string schemaVersion,
            CancellationToken ct = default
        ) => Task.FromResult<XmlSchemaSet?>(null);
    }

    private static ElectronicDocumentSchemaValidatorResolver BuildResolver() =>
        new(
            [
                new InvoiceXmlSchemaValidator(new NullSchemaProvider()),
                new CreditNoteXmlSchemaValidator(new NullSchemaProvider()),
                new RetentionXmlSchemaValidator(new NullSchemaProvider()),
            ]
        );

    [Fact]
    public void Resolve_returns_RetentionXmlSchemaValidator_for_Retention()
    {
        var resolver = BuildResolver();

        var validator = resolver.Resolve(ElectronicDocumentType.Retention);

        validator.Should().BeOfType<RetentionXmlSchemaValidator>();
        validator!.DocumentType.Should().Be(ElectronicDocumentType.Retention);
    }

    [Fact]
    public void Resolve_still_returns_InvoiceXmlSchemaValidator_for_Invoice()
    {
        var resolver = BuildResolver();

        var validator = resolver.Resolve(ElectronicDocumentType.Invoice);

        validator.Should().BeOfType<InvoiceXmlSchemaValidator>();
    }

    [Fact]
    public void Resolve_still_returns_CreditNoteXmlSchemaValidator_for_CreditNote()
    {
        var resolver = BuildResolver();

        var validator = resolver.Resolve(ElectronicDocumentType.CreditNote);

        validator.Should().BeOfType<CreditNoteXmlSchemaValidator>();
    }

    [Fact]
    public void Resolve_returns_null_for_a_type_without_a_registered_validator()
    {
        var resolver = BuildResolver();

        var validator = resolver.Resolve(ElectronicDocumentType.DebitNote);

        validator.Should().BeNull();
    }
}
