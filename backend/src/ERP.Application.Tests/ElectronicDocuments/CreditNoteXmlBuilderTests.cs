using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Infrastructure.Services.ElectronicDocuments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Xml;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// P0-01 Fase 8/10 — CreditNoteXmlBuilder. Valida el XML generado contra el XSD real embebido
/// (<c>NotaCredito_V1.1.0.xsd</c>) invocando <see cref="EmbeddedXmlSchemaProvider"/>
/// directamente. Desde ADR-031 (Fase 11) <c>manifest.json.activeVersion</c> ya es "1.1.0" para
/// CreditNote y <c>CreditNoteXmlSchemaValidator</c> está registrado en DI — este test
/// sigue invocando el provider directamente (en vez de pasar por el pipeline completo) porque
/// es más rápido y no requiere infraestructura HTTP/DI. Referenciar
/// <c>EmbeddedXmlSchemaProvider</c> (ERP.Infrastructure) es solo consumo de una clase ya
/// existente para verificación de tests — no se modifica ningún archivo de Infrastructure.
/// </summary>
public sealed class CreditNoteXmlBuilderTests
{
    private static Mock<ISriTaxCategoryCodeResolver> TaxResolver()
    {
        var mock = new Mock<ISriTaxCategoryCodeResolver>();
        mock.Setup(r => r.Resolve("VAT")).Returns("2");
        mock.Setup(r => r.Resolve("ICE")).Returns("3");
        return mock;
    }

    private static ElectronicDocumentData ValidData() =>
        new(
            Emission: new ElectronicDocumentEmissionContext(
                Environment: "1",
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
                    Code: "SKU-001",
                    Description: "Producto devuelto",
                    Quantity: 2m,
                    UnitPrice: 10m,
                    Discount: 0m,
                    Subtotal: 20m,
                    Taxes: [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)]
                ),
            ],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 20m, 3m)],
            Totals: new ElectronicDocumentTotals(20m, 0m, 3m, 23m, "USD"),
            Payments: [],
            AdditionalInfo: [],
            Reason: "Producto en mal estado",
            ModifiedDocument: new ElectronicDocumentModifiedReference(
                DocTypeCode: "01",
                Number: "001-001-000000045",
                IssueDate: new DateTime(2026, 7, 20)
            )
        );

    [Fact]
    public void Build_con_datos_validos_produce_xml_valido_contra_el_xsd_oficial()
    {
        var builder = new CreditNoteXmlBuilder(TaxResolver().Object);

        var result = builder.Build(ValidData());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.DocumentType.Should().Be(ElectronicDocumentType.CreditNote);
        result.Value.AccessKey.Should().HaveLength(49);

        ValidateAgainstOfficialXsd(result.Value.Xml);
    }

    [Fact]
    public void Build_incluye_el_motivo_y_la_referencia_al_comprobante_modificado()
    {
        var builder = new CreditNoteXmlBuilder(TaxResolver().Object);

        var result = builder.Build(ValidData());

        result.IsSuccess.Should().BeTrue();
        var xml = result.Value!.Xml;
        xml.Should().Contain("<motivo>Producto en mal estado</motivo>");
        xml.Should().Contain("<codDocModificado>01</codDocModificado>");
        xml.Should().Contain("<numDocModificado>001-001-000000045</numDocModificado>");
        xml.Should().Contain("<fechaEmisionDocSustento>20/07/2026</fechaEmisionDocSustento>");
    }

    [Fact]
    public void Build_rechaza_datos_sin_motivo()
    {
        var builder = new CreditNoteXmlBuilder(TaxResolver().Object);
        var data = ValidData() with { Reason = null };

        var result = builder.Build(data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("motivo");
    }

    [Fact]
    public void Build_rechaza_datos_sin_referencia_al_comprobante_modificado()
    {
        var builder = new CreditNoteXmlBuilder(TaxResolver().Object);
        var data = ValidData() with { ModifiedDocument = null };

        var result = builder.Build(data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("comprobante que modifica");
    }

    [Fact]
    public void Build_rechaza_codigo_de_impuesto_no_resoluble()
    {
        var resolver = new Mock<ISriTaxCategoryCodeResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns((string?)null);
        var builder = new CreditNoteXmlBuilder(resolver.Object);

        var result = builder.Build(ValidData());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("código de impuesto");
    }

    [Fact]
    public void Build_sin_detalles_falla()
    {
        var builder = new CreditNoteXmlBuilder(TaxResolver().Object);
        var data = ValidData() with { Details = [] };

        var result = builder.Build(data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("detalle");
    }

    // ── Validación real contra NotaCredito_V1.1.0.xsd ────────────────────

    private static void ValidateAgainstOfficialXsd(string xml)
    {
        var provider = new EmbeddedXmlSchemaProvider(
            NullLogger<EmbeddedXmlSchemaProvider>.Instance
        );
        var schemaSet = provider
            .GetSchemaSetAsync(ElectronicDocumentType.CreditNote, "1.1.0")
            .GetAwaiter()
            .GetResult();

        schemaSet.Should().NotBeNull("el XSD oficial de Nota de Crédito debe estar embebido y ser resoluble");

        var errors = new List<string>();
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemaSet!,
        };
        settings.ValidationEventHandler += (_, e) => errors.Add(e.Message);

        using var stringReader = new StringReader(xml);
        using var xmlReader = XmlReader.Create(stringReader, settings);
        while (xmlReader.Read()) { }

        errors.Should().BeEmpty(because: "el XML generado debe validar contra el XSD oficial del SRI sin errores");
    }
}
