using System.Xml;
using System.Xml.Linq;
using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.AdditionalInfo;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.SriCatalogs.Constants;
using ERP.Infrastructure.Services.ElectronicDocuments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments.AdditionalInfo;

/// <summary>
/// ZH-SRI-ANEXO26-PROVIDER-RUC-01 — de punta a punta sin mocks en la parte fiscal: orquestador real
/// (<see cref="CommercialElectronicDocumentXmlSupplier"/> / <see cref="RetentionElectronicDocumentXmlService"/>)
/// + composer y contributor reales + builders reales (sin cambios) → XSD oficial embebido → parser RIDE
/// existente (sin cambios). Solo el provider (datos del origen) y el repositorio de configuración son
/// dobles.
/// </summary>
public sealed class ProviderRucXmlComplianceTests
{
    private static readonly DateOnly IssueDate = new(2026, 11, 3);
    private static readonly ElectronicDocumentSourceReference Reference = new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid()
    );

    private sealed class FakeTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
    {
        public string? Resolve(string taxCode) =>
            taxCode switch
            {
                "VAT" => "2",
                "ICE" => "3",
                "IRBPNR" => "5",
                _ => null,
            };
    }

    // ── Escenarios de configuración ──────────────────────────────────────

    /// <summary>Requisito exigible (EffectiveDate = IssueDate) y configuración completa.</summary>
    private static IElectronicDocumentAdditionalInfoComposer Exigible() =>
        AdditionalInfoTestDoubles.ComposerWithProviderRuc(
            AdditionalInfoTestDoubles.ProviderSettings(true, IssueDate)
        );

    /// <summary>Emisión anterior a EffectiveDate: el requisito aún no aplica.</summary>
    private static IElectronicDocumentAdditionalInfoComposer AntesDeLaFecha() =>
        AdditionalInfoTestDoubles.ComposerWithProviderRuc(
            AdditionalInfoTestDoubles.ProviderSettings(true, IssueDate.AddDays(1))
        );

    // ── Factura (01) ─────────────────────────────────────────────────────

    private static ElectronicDocumentData InvoiceData(
        IReadOnlyList<ElectronicDocumentAdditionalField> additionalInfo
    ) =>
        new(
            Emission: new ElectronicDocumentEmissionContext(
                "1",
                "1",
                "01",
                "001",
                "Av. Amazonas",
                "001",
                "000000123",
                IssueDate
            ),
            Issuer: new ElectronicDocumentIssuerData(
                "1790012345001",
                "ACME CIA LTDA",
                "ACME",
                "Av. Amazonas",
                null,
                true
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                "05",
                "1710034065",
                "Juan Perez",
                "Calle Falsa 123",
                "juan@example.com"
            ),
            Details:
            [
                new ElectronicDocumentDetailLine(
                    "SKU-001",
                    "Producto de prueba",
                    2m,
                    10m,
                    0m,
                    20m,
                    [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)]
                ),
            ],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 20m, 3m)],
            Totals: new ElectronicDocumentTotals(20m, 0m, 3m, 23m, "USD"),
            Payments: [new ElectronicDocumentPayment("01", 23m, null, null)],
            AdditionalInfo: additionalInfo
        );

    private static ElectronicDocumentData CreditNoteData() =>
        InvoiceData([]) with
        {
            Emission = new ElectronicDocumentEmissionContext(
                "1",
                "1",
                "04",
                "001",
                "Av. Amazonas",
                "001",
                "000000007",
                IssueDate
            ),
            Payments = [],
            Reason = "Producto en mal estado",
            ModifiedDocument = new ElectronicDocumentModifiedReference(
                "01",
                "001-001-000000123",
                IssueDate.AddDays(-5)
            ),
        };

    private static async Task<Result<ElectronicDocumentXml>> BuildCommercialAsync(
        ElectronicDocumentType type,
        ElectronicDocumentData data,
        IElectronicDocumentAdditionalInfoComposer composer
    )
    {
        var provider = new Mock<IElectronicDocumentDataProvider>();
        provider
            .Setup(p => p.GetDataAsync(Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ElectronicDocumentData>.Success(data));
        IElectronicDocumentXmlBuilder builder =
            type == ElectronicDocumentType.Invoice
                ? new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver())
                : new CreditNoteXmlBuilder(new FakeTaxCategoryCodeResolver());

        return await new CommercialElectronicDocumentXmlSupplier(
            type,
            provider.Object,
            builder,
            composer
        ).BuildXmlAsync(Reference, CancellationToken.None);
    }

    [Fact]
    public async Task Factura_emite_RUC_Proveedor_y_Observacion_en_orden_determinista_y_pasa_el_XSD_1_1_0()
    {
        var data = InvoiceData([
            new ElectronicDocumentAdditionalField("Observación", "Entregar en bodega"),
        ]);

        var result = await BuildCommercialAsync(ElectronicDocumentType.Invoice, data, Exigible());

        result.IsSuccess.Should().BeTrue(result.Error);
        AdditionalFields(result.Value!.Xml)
            .Should()
            .Equal(
                (
                    SriAdditionalInfoFieldNames.SystemProviderRuc,
                    AdditionalInfoTestDoubles.ProviderRuc
                ),
                ("Observación", "Entregar en bodega")
            );
        ValidateAgainstOfficialXsd(result.Value.Xml, ElectronicDocumentType.Invoice, "1.1.0");

        var ride = new InvoiceRideXmlParser().Parse(result.Value.Xml);
        ride.IsSuccess.Should().BeTrue(ride.Error);
        ride.Value!.AdditionalInfo.Select(a => (a.Name, a.Value))
            .Should()
            .Contain(
                (
                    SriAdditionalInfoFieldNames.SystemProviderRuc,
                    AdditionalInfoTestDoubles.ProviderRuc
                )
            );
    }

    [Fact]
    public async Task Factura_antes_de_EffectiveDate_produce_exactamente_el_XML_actual()
    {
        var data = InvoiceData([
            new ElectronicDocumentAdditionalField("Observación", "Entregar en bodega"),
        ]);

        var withRule = await BuildCommercialAsync(
            ElectronicDocumentType.Invoice,
            data,
            AntesDeLaFecha()
        );
        var current = await BuildCommercialAsync(
            ElectronicDocumentType.Invoice,
            data,
            AdditionalInfoTestDoubles.PassThroughComposer()
        );

        withRule.IsSuccess.Should().BeTrue(withRule.Error);
        withRule.Value!.Xml.Should().Be(current.Value!.Xml);
        AdditionalFields(withRule.Value.Xml).Should().Equal(("Observación", "Entregar en bodega"));
    }

    [Fact]
    public async Task Nota_de_credito_emite_RUC_Proveedor_pasa_el_XSD_1_1_0_y_lo_muestra_el_RIDE()
    {
        var result = await BuildCommercialAsync(
            ElectronicDocumentType.CreditNote,
            CreditNoteData(),
            Exigible()
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        AdditionalFields(result.Value!.Xml)
            .Should()
            .Equal(
                (
                    SriAdditionalInfoFieldNames.SystemProviderRuc,
                    AdditionalInfoTestDoubles.ProviderRuc
                )
            );
        ValidateAgainstOfficialXsd(result.Value.Xml, ElectronicDocumentType.CreditNote, "1.1.0");

        var ride = new CreditNoteRideXmlParser().Parse(result.Value.Xml);
        ride.IsSuccess.Should().BeTrue(ride.Error);
        ride.Value!.AdditionalInfo.Select(a => (a.Name, a.Value))
            .Should()
            .Equal(
                (
                    SriAdditionalInfoFieldNames.SystemProviderRuc,
                    AdditionalInfoTestDoubles.ProviderRuc
                )
            );
    }

    [Fact]
    public async Task Nota_de_credito_antes_de_EffectiveDate_produce_exactamente_el_XML_actual()
    {
        var withRule = await BuildCommercialAsync(
            ElectronicDocumentType.CreditNote,
            CreditNoteData(),
            AntesDeLaFecha()
        );
        var current = await BuildCommercialAsync(
            ElectronicDocumentType.CreditNote,
            CreditNoteData(),
            AdditionalInfoTestDoubles.PassThroughComposer()
        );

        withRule.Value!.Xml.Should().Be(current.Value!.Xml);
        withRule.Value.Xml.Should().NotContain("infoAdicional");
    }

    [Fact]
    public async Task Configuracion_exigible_incompleta_no_genera_XML()
    {
        var composer = AdditionalInfoTestDoubles.ComposerWithProviderRuc(
            AdditionalInfoTestDoubles.ProviderSettings(false, IssueDate)
        );

        var result = await BuildCommercialAsync(
            ElectronicDocumentType.Invoice,
            InvoiceData([]),
            composer
        );

        result.IsSuccess.Should().BeFalse();
        result
            .Code.Should()
            .Be(
                ERP.Application
                    .Common
                    .ApiResponseCodes
                    .ElectronicDocuments
                    .SystemProviderRucNotConfigured
            );
    }

    // ── Retención (07) ───────────────────────────────────────────────────

    private static RetentionElectronicDocumentData RetentionData() =>
        new(
            Metadata: new RetentionElectronicDocumentMetadata(
                Guid.NewGuid(),
                Reference.TenantId,
                Reference.CompanyId,
                Guid.NewGuid(),
                RetentionSourceDocumentType.ExpenseDocument,
                Guid.NewGuid(),
                new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc)
            ),
            Emission: new ElectronicDocumentEmissionContext(
                "1",
                "1",
                "07",
                "001",
                "Av. Principal 123",
                "001",
                "000000001",
                IssueDate
            ),
            NumeroCompleto: "001-001-000000001",
            Issuer: new ElectronicDocumentIssuerData(
                "1790012345001",
                "Empresa Test S.A.",
                "Empresa Test",
                "Matriz 456",
                null,
                true
            ),
            RetentionInfo: new RetentionElectronicDocumentInfo(null, "11/2026"),
            SubjectWithheld: new ElectronicDocumentCounterpartyData(
                "04",
                "1792146739001",
                "Proveedor Test",
                null,
                null
            ),
            SourceDocument: new RetentionElectronicDocumentSourceDocument(
                "01",
                "01",
                "001-001-000000456",
                "1234567890",
                IssueDate.AddDays(-2),
                100m,
                115m
            ),
            Lines:
            [
                new RetentionElectronicDocumentTaxLine(
                    RetentionTaxType.Vat,
                    "2",
                    "1",
                    "Ret. IVA 30%",
                    15m,
                    30m,
                    4.5m
                ),
                new RetentionElectronicDocumentTaxLine(
                    RetentionTaxType.Income,
                    "1",
                    "303",
                    "Honorarios profesionales",
                    100m,
                    10m,
                    10m
                ),
            ],
            Totals: new RetentionElectronicDocumentTotals(4.5m, 10m, 14.5m),
            AdditionalInfo: []
        );

    private static RetentionElectronicDocumentXmlService RetentionService(
        IElectronicDocumentAdditionalInfoComposer composer
    )
    {
        var provider = new Mock<IRetentionElectronicDocumentDataProvider>();
        provider
            .Setup(p => p.GetDataAsync(Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RetentionElectronicDocumentData>.Success(RetentionData()));
        return new RetentionElectronicDocumentXmlService(
            provider.Object,
            new RetentionXmlBuilder(),
            composer
        );
    }

    [Fact]
    public async Task Retencion_emite_RUC_Proveedor_pasa_el_XSD_1_0_0_y_lo_muestra_el_RIDE()
    {
        var result = await RetentionService(Exigible()).GenerateXmlAsync(Reference);

        result.IsSuccess.Should().BeTrue(result.Error);
        AdditionalFields(result.Value!.Xml)
            .Should()
            .Equal(
                (
                    SriAdditionalInfoFieldNames.SystemProviderRuc,
                    AdditionalInfoTestDoubles.ProviderRuc
                )
            );
        ValidateAgainstOfficialXsd(result.Value.Xml, ElectronicDocumentType.Retention, "1.0.0");

        var ride = new RetentionRideXmlParser().Parse(result.Value.Xml);
        ride.IsSuccess.Should().BeTrue(ride.Error);
        ride.Value!.AdditionalInfo.Select(a => (a.Name, a.Value))
            .Should()
            .Equal(
                (
                    SriAdditionalInfoFieldNames.SystemProviderRuc,
                    AdditionalInfoTestDoubles.ProviderRuc
                )
            );
    }

    [Fact]
    public async Task Retencion_antes_de_EffectiveDate_produce_exactamente_el_XML_actual()
    {
        var withRule = await RetentionService(AntesDeLaFecha()).GenerateXmlAsync(Reference);
        var current = await RetentionService(AdditionalInfoTestDoubles.PassThroughComposer())
            .GenerateXmlAsync(Reference);

        withRule.Value!.Xml.Should().Be(current.Value!.Xml);
        withRule.Value.Xml.Should().NotContain("infoAdicional");
    }

    [Fact]
    public async Task Retencion_la_vista_previa_y_el_pipeline_producen_el_mismo_XML()
    {
        // Vista previa XML (GenerateRetentionXmlUseCases) y RIDE de vista previa
        // (GenerateRetentionRidePdfUseCases) consumen IRetentionElectronicDocumentXmlService; el
        // pipeline lo consume vía RetentionElectronicDocumentXmlSupplier. Mismo servicio → mismo XML.
        var service = RetentionService(Exigible());

        var preview = await service.GenerateXmlAsync(Reference);
        var pipeline = await new RetentionElectronicDocumentXmlSupplier(service).BuildXmlAsync(
            Reference
        );

        pipeline.IsSuccess.Should().BeTrue(pipeline.Error);
        pipeline.Value!.Xml.Should().Be(preview.Value!.Xml);
        pipeline.Value.Xml.Should().Contain(SriAdditionalInfoFieldNames.SystemProviderRuc);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static IReadOnlyList<(string Name, string Value)> AdditionalFields(string xml) =>
        XDocument
            .Parse(xml)
            .Root!.Element("infoAdicional")
            ?.Elements("campoAdicional")
            .Select(e => (e.Attribute("nombre")!.Value, e.Value))
            .ToList()
        ?? [];

    private static void ValidateAgainstOfficialXsd(
        string xml,
        ElectronicDocumentType type,
        string version
    )
    {
        var schemaSet = new EmbeddedXmlSchemaProvider(
            NullLogger<EmbeddedXmlSchemaProvider>.Instance
        )
            .GetSchemaSetAsync(type, version)
            .GetAwaiter()
            .GetResult();
        schemaSet.Should().NotBeNull($"el XSD oficial {type} {version} debe estar embebido");

        var errors = new List<string>();
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemaSet!,
        };
        settings.ValidationEventHandler += (_, e) => errors.Add(e.Message);
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        while (reader.Read()) { }

        errors
            .Should()
            .BeEmpty(
                $"el XML con RUC Proveedor debe validar contra el XSD oficial {type} {version}"
            );
    }
}
