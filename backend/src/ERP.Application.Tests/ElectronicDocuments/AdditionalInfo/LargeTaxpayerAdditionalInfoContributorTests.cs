using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.AdditionalInfo;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.SriCatalogs.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Application.Tests.ElectronicDocuments.AdditionalInfo;

/// <summary>R22 (Ficha 2.34, Anexo 24) — campo adicional "Gran Contribuyente".</summary>
public sealed class LargeTaxpayerAdditionalInfoContributorTests
{
    private const string Resolution = "NAC-GCFOIOC21-00000868-E";

    private static AdditionalInfoCompositionContext Context(
        string? resolution,
        ElectronicDocumentType type = ElectronicDocumentType.Invoice
    ) =>
        new(
            type,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 11, 3),
            new ElectronicDocumentIssuerData(
                "1790012345001",
                "Emisor S.A.",
                null,
                "Matriz",
                null,
                true,
                LargeTaxpayerResolutionNumber: resolution
            )
        );

    [Theory]
    [InlineData(ElectronicDocumentType.Invoice, true)]
    [InlineData(ElectronicDocumentType.PurchaseSettlement, true)]
    [InlineData(ElectronicDocumentType.CreditNote, true)]
    [InlineData(ElectronicDocumentType.DebitNote, true)]
    [InlineData(ElectronicDocumentType.Retention, false)]
    [InlineData(ElectronicDocumentType.ShippingGuide, false)]
    public void Aplica_solo_a_comprobantes_de_venta_notas_de_credito_y_de_debito(
        ElectronicDocumentType type,
        bool applies
    ) => new LargeTaxpayerAdditionalInfoContributor().AppliesTo(type).Should().Be(applies);

    [Fact]
    public async Task Con_resolucion_aplicable_emite_el_nombre_normativo_y_la_resolucion_tal_cual()
    {
        var result = await new LargeTaxpayerAdditionalInfoContributor().ContributeAsync(
            Context(Resolution)
        );

        result.IsSuccess.Should().BeTrue();
        result
            .Value!.Select(f => (f.Name, f.Value))
            .Should()
            .Equal(("Gran Contribuyente", Resolution));
        SriAdditionalInfoFieldNames.LargeTaxpayer.Should().Be("Gran Contribuyente");
    }

    [Fact]
    public async Task Sin_resolucion_aplicable_no_aporta_campos()
    {
        var result = await new LargeTaxpayerAdditionalInfoContributor().ContributeAsync(
            Context(null)
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task El_nombre_es_reservado_un_campo_del_documento_no_puede_suplantarlo()
    {
        var composer = new ElectronicDocumentAdditionalInfoComposer(
            [new LargeTaxpayerAdditionalInfoContributor()],
            NullLogger<ElectronicDocumentAdditionalInfoComposer>.Instance
        );

        var result = await composer.ComposeAsync(
            Context(Resolution),
            [new ElectronicDocumentAdditionalField("gran contribuyente", "otro valor")]
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.AdditionalInfoInvalid);
    }

    [Fact]
    public async Task En_la_retencion_el_composer_no_lo_incluye()
    {
        var composer = new ElectronicDocumentAdditionalInfoComposer(
            [new LargeTaxpayerAdditionalInfoContributor()],
            NullLogger<ElectronicDocumentAdditionalInfoComposer>.Instance
        );

        var result = await composer.ComposeAsync(
            Context(Resolution, ElectronicDocumentType.Retention),
            []
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
