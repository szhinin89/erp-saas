using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Infrastructure.Services.ElectronicDocuments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Xml;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// RETENTIONS-SRI-XML-MAPPER-03B — RetentionXmlBuilder. Valida el XML generado contra el XSD
/// real embebido (<c>ComprobanteRetencion_V1.0.0.xsd</c>) invocando
/// <see cref="EmbeddedXmlSchemaProvider"/> directamente, mismo patrón que
/// <c>InvoiceXmlBuilderTests</c>/<c>CreditNoteXmlBuilderTests</c>. <c>manifest.json.Retention.activeVersion</c>
/// sigue en <c>null</c> — la resolución del schema set aquí es por (tipo, versión) explícita, no
/// depende de <c>activeVersion</c>, así que este test no requiere ni provoca activar el esquema.
/// </summary>
public sealed class RetentionXmlBuilderTests
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

    [Fact]
    public void Build_con_datos_validos_produce_xml_valido_contra_el_xsd_oficial()
    {
        var builder = new RetentionXmlBuilder();

        var result = builder.Build(ValidData());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.DocumentType.Should().Be(ElectronicDocumentType.Retention);
        result.Value.AccessKey.Should().HaveLength(49);

        ValidateAgainstOfficialXsd(result.Value.Xml);
    }

    [Fact]
    public void Build_usa_codDoc_07()
    {
        var builder = new RetentionXmlBuilder();

        var result = builder.Build(ValidData());

        result.Value!.Xml.Should().Contain("<codDoc>07</codDoc>");
    }

    [Fact]
    public void Build_incluye_numero_estab_ptoEmi_secuencial()
    {
        var builder = new RetentionXmlBuilder();

        var result = builder.Build(ValidData());

        var xml = result.Value!.Xml;
        xml.Should().Contain("<estab>001</estab>");
        xml.Should().Contain("<ptoEmi>001</ptoEmi>");
        xml.Should().Contain("<secuencial>000000001</secuencial>");
    }

    [Fact]
    public void Build_rechaza_secuencial_que_no_tiene_9_digitos()
    {
        var builder = new RetentionXmlBuilder();
        var data = ValidData() with { Emission = ValidData().Emission with { Sequential = "1" } };

        var result = builder.Build(data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("secuencial");
    }

    [Fact]
    public void Build_incluye_periodo_fiscal()
    {
        var builder = new RetentionXmlBuilder();

        var result = builder.Build(ValidData());

        result.Value!.Xml.Should().Contain("<periodoFiscal>08/2026</periodoFiscal>");
    }

    [Fact]
    public void Build_incluye_el_documento_sustento_en_cada_linea_de_impuesto()
    {
        var builder = new RetentionXmlBuilder();

        var result = builder.Build(ValidData());

        var xml = result.Value!.Xml;
        xml.Should().Contain("<codDocSustento>01</codDocSustento>");
        // "001-001-000000456" sin guiones = 15 dígitos, cumple el patrón numDocSustento del XSD 1.0.0.
        xml.Should().Contain("<numDocSustento>001001000000456</numDocSustento>");
        xml.Should().Contain("<fechaEmisionDocSustento>01/08/2026</fechaEmisionDocSustento>");
    }

    [Fact]
    public void Build_omite_numDocSustento_si_el_numero_no_tiene_15_digitos()
    {
        var builder = new RetentionXmlBuilder();
        var data = ValidData() with
        {
            SourceDocument = ValidData().SourceDocument with { Number = "ABC-123" },
        };

        var result = builder.Build(data);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Xml.Should().NotContain("<numDocSustento>");
    }

    [Fact]
    public void Build_incluye_las_lineas_de_iva_y_renta_con_sus_valores()
    {
        var builder = new RetentionXmlBuilder();

        var result = builder.Build(ValidData());

        var xml = result.Value!.Xml;
        xml.Should().Contain("<codigo>2</codigo>");
        xml.Should().Contain("<codigoRetencion>725</codigoRetencion>");
        xml.Should().Contain("<baseImponible>15.00</baseImponible>");
        xml.Should().Contain("<porcentajeRetener>30.00</porcentajeRetener>");
        xml.Should().Contain("<valorRetenido>4.50</valorRetenido>");
        xml.Should().Contain("<codigo>1</codigo>");
        xml.Should().Contain("<codigoRetencion>303</codigoRetencion>");
        xml.Should().Contain("<valorRetenido>8.00</valorRetenido>");
    }

    [Fact]
    public void Build_genera_una_clave_de_acceso_de_49_digitos_sin_datos_quemados()
    {
        var builder = new RetentionXmlBuilder();
        var data = ValidData();

        var result = builder.Build(data);
        var otherResult = builder.Build(
            data with
            {
                Emission = data.Emission with { Sequential = "000000002" },
            }
        );

        result.Value!.AccessKey.Should().MatchRegex("^[0-9]{49}$");
        // Distinto secuencial → distinta clave de acceso: nunca un valor fijo/quemado.
        result.Value.AccessKey.Should().NotBe(otherResult.Value!.AccessKey);
    }

    [Fact]
    public void Build_rechaza_datos_sin_lineas_de_retencion()
    {
        var builder = new RetentionXmlBuilder();
        var data = ValidData() with { Lines = [] };

        var result = builder.Build(data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("línea");
    }

    // ── Validación real contra ComprobanteRetencion_V1.0.0.xsd ──────────────

    private static void ValidateAgainstOfficialXsd(string xml)
    {
        var provider = new EmbeddedXmlSchemaProvider(
            NullLogger<EmbeddedXmlSchemaProvider>.Instance
        );
        var schemaSet = provider
            .GetSchemaSetAsync(ElectronicDocumentType.Retention, "1.0.0")
            .GetAwaiter()
            .GetResult();

        schemaSet
            .Should()
            .NotBeNull(
                "el XSD oficial de Comprobante de Retención (1.0.0) debe estar embebido y ser resoluble"
            );

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

        errors
            .Should()
            .BeEmpty(
                because: "el XML generado debe validar contra el XSD oficial del SRI sin errores"
            );
    }
}
