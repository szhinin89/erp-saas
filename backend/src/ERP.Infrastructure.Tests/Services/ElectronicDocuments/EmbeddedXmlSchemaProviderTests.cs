using System.Xml;
using System.Xml.Schema;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Infrastructure.Services.ElectronicDocuments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Services.ElectronicDocuments;

/// <summary>
/// Cubre la resolución guiada por <c>manifest.json</c> (ver
/// ElectronicDocuments/Resources/SRI/manifest.json) — nunca por una convención de nombre de
/// archivo derivada del tipo/versión, ya que los XSD del SRI no siguen un patrón consistente
/// entre comprobantes.
/// </summary>
public sealed class EmbeddedXmlSchemaProviderTests
{
    private static EmbeddedXmlSchemaProvider BuildProvider() =>
        new(NullLogger<EmbeddedXmlSchemaProvider>.Instance);

    [Fact]
    public async Task GetSchemaSetAsync_for_unknown_document_type_returns_null()
    {
        var provider = BuildProvider();

        var result = await provider.GetSchemaSetAsync((ElectronicDocumentType)999, "1.1.0");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSchemaSetAsync_for_version_not_in_manifest_returns_null()
    {
        var provider = BuildProvider();

        var result = await provider.GetSchemaSetAsync(ElectronicDocumentType.Invoice, "9.9.9");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSchemaSetAsync_for_manifest_entry_resolves_including_common_dependency()
    {
        // Invoice 1.1.0 está en el manifiesto, su XSD principal existe como recurso embebido, y
        // su dependencia declarada (Common/xmldsig-core-schema.xsd) también — el set debe
        // compilar con ambos esquemas cargados.
        var provider = BuildProvider();

        var result = await provider.GetSchemaSetAsync(ElectronicDocumentType.Invoice, "1.1.0");

        result.Should().NotBeNull();
        result!.Count.Should().Be(2); // XSD principal + Common/xmldsig-core-schema.xsd
    }

    [Theory]
    [InlineData(ElectronicDocumentType.CreditNote, "1.1.0")]
    [InlineData(ElectronicDocumentType.DebitNote, "1.0.0")]
    [InlineData(ElectronicDocumentType.Retention, "2.0.0")]
    [InlineData(ElectronicDocumentType.ShippingGuide, "1.1.0")]
    [InlineData(ElectronicDocumentType.PurchaseSettlement, "1.1.0")]
    public async Task GetSchemaSetAsync_resolves_manifest_entry_for_every_document_type_present(
        ElectronicDocumentType documentType,
        string version
    )
    {
        // Todos los tipos declaran la misma dependencia común (Common/xmldsig-core-schema.xsd,
        // ver manifest.json) — con ella embebida y los XSD propios bien formados, ninguno debe
        // quedar en null por "tipo/versión no encontrado en el manifiesto".
        var provider = BuildProvider();

        var result = await provider.GetSchemaSetAsync(documentType, version);

        result.Should().NotBeNull();
        result!.Count.Should().Be(2); // XSD principal + Common/xmldsig-core-schema.xsd
    }

    // ── R20 (docs/sri/SRI-2.34-COMPLIANCE-CLOSURE.md): leyenda RIMPE frente a los XSD activos ──
    // Los XSD embebidos de las versiones activas coinciden con los publicados por el SRI (paquetes
    // de febrero 2022) y solo admiten la leyenda de RIMPE Emprendedor. La ficha 2.34 (Anexo 22,
    // cambio 2.22 del 01/09/2022) exige además la de Negocio Popular. Si el SRI publica un XSD que
    // la admita, NegocioPopular_legend_is_rejected falla y R20 debe reabrirse.

    private const string RimpeEmprendedorLegend = "CONTRIBUYENTE RÉGIMEN RIMPE";
    private const string RimpeNegocioPopularLegend = "CONTRIBUYENTE NEGOCIO POPULAR - RÉGIMEN RIMPE";

    private static async Task<XmlSchemaSimpleType> ContribuyenteRimpeTypeAsync(
        ElectronicDocumentType documentType,
        string version
    )
    {
        var set = await BuildProvider().GetSchemaSetAsync(documentType, version);
        set.Should().NotBeNull();
        return (XmlSchemaSimpleType)set!.GlobalTypes[new XmlQualifiedName("contribuyenteRimpe")]!;
    }

    private static bool AcceptsValue(XmlSchemaSimpleType type, string value)
    {
        try
        {
            type.Datatype!.ParseValue(value, new NameTable(), null);
            return true;
        }
        catch (XmlSchemaException)
        {
            return false;
        }
    }

    [Theory]
    [InlineData(ElectronicDocumentType.Invoice, "1.1.0")]
    [InlineData(ElectronicDocumentType.CreditNote, "1.1.0")]
    [InlineData(ElectronicDocumentType.Retention, "1.0.0")]
    public async Task Active_schema_accepts_the_RIMPE_Emprendedor_legend(
        ElectronicDocumentType documentType,
        string version
    )
    {
        var type = await ContribuyenteRimpeTypeAsync(documentType, version);

        AcceptsValue(type, RimpeEmprendedorLegend).Should().BeTrue();
        AcceptsValue(type, "RIMPE_ME").Should().BeFalse();
    }

    [Theory]
    [InlineData(ElectronicDocumentType.Invoice, "1.1.0")]
    [InlineData(ElectronicDocumentType.CreditNote, "1.1.0")]
    [InlineData(ElectronicDocumentType.Retention, "1.0.0")]
    public async Task Active_schema_rejects_the_NegocioPopular_legend_required_by_Anexo_22(
        ElectronicDocumentType documentType,
        string version
    )
    {
        var type = await ContribuyenteRimpeTypeAsync(documentType, version);

        AcceptsValue(type, RimpeNegocioPopularLegend).Should().BeFalse();
    }

    [Theory]
    [InlineData(ElectronicDocumentType.Invoice, "1.1.0")]
    [InlineData(ElectronicDocumentType.CreditNote, "1.1.0")]
    [InlineData(ElectronicDocumentType.Retention, "1.0.0")]
    public async Task Active_schema_keeps_contribuyenteRimpe_optional_for_non_RIMPE_regimes(
        ElectronicDocumentType documentType,
        string version
    )
    {
        var set = await BuildProvider().GetSchemaSetAsync(documentType, version);
        var infoTributaria = (XmlSchemaComplexType)
            set!.GlobalTypes[new XmlQualifiedName("infoTributaria")]!;
        var element = ((XmlSchemaSequence)infoTributaria.ContentTypeParticle)
            .Items.OfType<XmlSchemaElement>()
            .Single(e => e.Name == "contribuyenteRimpe");

        element.MinOccurs.Should().Be(0);
    }
}
