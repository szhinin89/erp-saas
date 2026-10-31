using System.Xml.Linq;
using ERP.Application.Modules.ElectronicDocuments.AdditionalInfo;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Tests.ElectronicDocuments.AdditionalInfo;
using ERP.Application.Tests.TestSupport;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.SriCatalogs.Constants;
using FluentAssertions;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;
using Fixtures = ERP.Application.Tests.ElectronicDocuments.AdditionalInfo.ProviderRucXmlComplianceTests;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// R21/R22 (Ficha 2.34, Anexos 21 y 24) y R23 en el RIDE — de punta a punta sin dobles en la parte
/// fiscal: Company (dominio real) → <see cref="ElectronicDocumentIssuerProjection"/> → orquestador,
/// composer y contributors de producción → builders → XSD oficial embebido → parser RIDE.
/// </summary>
public sealed class IssuerFiscalDesignationXmlComplianceTests
{
    private const string RetentionAgentNo = "6498";
    private const string LargeTaxpayerNo = "NAC-GCFOIOC21-00000868-E";

    private static readonly DateOnly IssueDate = Fixtures.IssueDate;

    /// <summary>Anexo 26 exigible: el RUC Proveedor convive con Gran Contribuyente.</summary>
    private static IElectronicDocumentAdditionalInfoComposer Composer() =>
        AdditionalInfoTestDoubles.ProductionComposer(
            AdditionalInfoTestDoubles.ProviderSettings(true, IssueDate)
        );

    private static ElectronicDocumentIssuerData Issuer(DateOnly? designatedFrom)
    {
        var company = CompanyEntity.CreateManaged(
            Fixtures.Reference.TenantId,
            "1790012345001",
            "ACME CIA LTDA",
            createdBy: Guid.NewGuid()
        );
        company.UpdateFiscalSettings(null, true, "5368", false, true, true, null);
        if (designatedFrom is not null)
            company.UpdateFiscalDesignations(
                "00006498",
                designatedFrom,
                LargeTaxpayerNo,
                designatedFrom,
                null
            );
        return ElectronicDocumentIssuerProjection.From(company, "Av. Amazonas", IssueDate);
    }

    private static string? InfoTributaria(string xml, string element) =>
        XDocument.Parse(xml).Root!.Element("infoTributaria")!.Element(element)?.Value;

    // ── Factura (01) ─────────────────────────────────────────────────────

    [Fact]
    public async Task Factura_de_emisor_designado_emite_agenteRetencion_y_Gran_Contribuyente_pasa_el_XSD_y_el_RIDE_los_muestra()
    {
        var data = Fixtures.InvoiceData([
            new ElectronicDocumentAdditionalField("Observación", "Entregar en bodega"),
        ]) with
        {
            Issuer = Issuer(designatedFrom: IssueDate),
        };

        var result = await Fixtures.BuildCommercialAsync(
            ElectronicDocumentType.Invoice,
            data,
            Composer()
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        InfoTributaria(result.Value!.Xml, "agenteRetencion").Should().Be(RetentionAgentNo);
        Fixtures
            .AdditionalFields(result.Value.Xml)
            .Should()
            .Equal(
                (SriAdditionalInfoFieldNames.LargeTaxpayer, LargeTaxpayerNo),
                (SriAdditionalInfoFieldNames.SystemProviderRuc, AdditionalInfoTestDoubles.ProviderRuc),
                ("Observación", "Entregar en bodega")
            );
        Fixtures.ValidateAgainstOfficialXsd(result.Value.Xml, ElectronicDocumentType.Invoice, "1.1.0");

        var ride = new InvoiceRideXmlParser().Parse(result.Value.Xml);
        ride.IsSuccess.Should().BeTrue(ride.Error);
        ride.Value!.Issuer.RetentionAgentResolution.Should().Be(RetentionAgentNo);
        ride.Value.Issuer.SpecialTaxpayerNumber.Should().Be("5368");
        ride.Value.AdditionalInfo.Select(a => (a.Name, a.Value))
            .Should()
            .Contain((SriAdditionalInfoFieldNames.LargeTaxpayer, LargeTaxpayerNo));
    }

    [Fact]
    public async Task Factura_anterior_a_la_vigencia_produce_exactamente_el_XML_sin_designacion()
    {
        var before = Fixtures.InvoiceData([]) with { Issuer = Issuer(IssueDate.AddDays(1)) };
        var none = Fixtures.InvoiceData([]) with { Issuer = Issuer(designatedFrom: null) };

        var withDesignation = await Fixtures.BuildCommercialAsync(
            ElectronicDocumentType.Invoice,
            before,
            Composer()
        );
        var withoutDesignation = await Fixtures.BuildCommercialAsync(
            ElectronicDocumentType.Invoice,
            none,
            Composer()
        );

        withDesignation.IsSuccess.Should().BeTrue(withDesignation.Error);
        withDesignation.Value!.Xml.Should().Be(withoutDesignation.Value!.Xml);
        withDesignation.Value.Xml.Should().NotContain("agenteRetencion");
        withDesignation.Value.Xml.Should().NotContain(SriAdditionalInfoFieldNames.LargeTaxpayer);
    }

    // ── Nota de crédito (04) ─────────────────────────────────────────────

    [Fact]
    public async Task Nota_de_credito_de_emisor_designado_emite_ambos_pasa_el_XSD_y_el_RIDE_los_muestra()
    {
        var data = Fixtures.CreditNoteData() with { Issuer = Issuer(designatedFrom: IssueDate) };

        var result = await Fixtures.BuildCommercialAsync(
            ElectronicDocumentType.CreditNote,
            data,
            Composer()
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        InfoTributaria(result.Value!.Xml, "agenteRetencion").Should().Be(RetentionAgentNo);
        Fixtures
            .AdditionalFields(result.Value.Xml)
            .Should()
            .Equal(
                (SriAdditionalInfoFieldNames.LargeTaxpayer, LargeTaxpayerNo),
                (SriAdditionalInfoFieldNames.SystemProviderRuc, AdditionalInfoTestDoubles.ProviderRuc)
            );
        Fixtures.ValidateAgainstOfficialXsd(
            result.Value.Xml,
            ElectronicDocumentType.CreditNote,
            "1.1.0"
        );

        var ride = new CreditNoteRideXmlParser().Parse(result.Value.Xml);
        ride.IsSuccess.Should().BeTrue(ride.Error);
        ride.Value!.Issuer.RetentionAgentResolution.Should().Be(RetentionAgentNo);
        ride.Value.Issuer.SpecialTaxpayerNumber.Should().Be("5368");
    }

    // ── Retención (07) ───────────────────────────────────────────────────

    [Fact]
    public async Task Retencion_de_emisor_designado_emite_agenteRetencion_pero_nunca_Gran_Contribuyente()
    {
        // Anexo 21 aplica a comprobantes de retención; el Anexo 24 solo a venta, NC y ND.
        var data = Fixtures.RetentionData() with { Issuer = Issuer(designatedFrom: IssueDate) };

        var result = await Fixtures.RetentionService(Composer(), data).GenerateXmlAsync(
            Fixtures.Reference
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        InfoTributaria(result.Value!.Xml, "agenteRetencion").Should().Be(RetentionAgentNo);
        Fixtures
            .AdditionalFields(result.Value.Xml)
            .Should()
            .Equal((SriAdditionalInfoFieldNames.SystemProviderRuc, AdditionalInfoTestDoubles.ProviderRuc));
        Fixtures.ValidateAgainstOfficialXsd(
            result.Value.Xml,
            ElectronicDocumentType.Retention,
            "1.0.0"
        );

        var ride = new RetentionRideXmlParser().Parse(result.Value.Xml);
        ride.IsSuccess.Should().BeTrue(ride.Error);
        ride.Value!.Issuer.RetentionAgentResolution.Should().Be(RetentionAgentNo);
        ride.Value.Issuer.SpecialTaxpayerNumber.Should().Be("5368");
    }
}
