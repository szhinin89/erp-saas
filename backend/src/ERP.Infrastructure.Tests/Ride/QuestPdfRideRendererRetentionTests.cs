using ERP.Application.Modules.Ride.Rendering;
using ERP.Infrastructure.Ride.Rendering;
using FluentAssertions;
using System.Text;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// RETENTIONS-RIDE-PDF-RENDERER-03D — <see cref="QuestPdfRideRenderer"/> real, con
/// <see cref="RetentionRideDocumentLayout"/> real (XML real → parser real → template real, ver
/// <see cref="RetentionRideRenderingFixtures"/>). No hay infraestructura de extracción de texto
/// de PDF en el repositorio (ver <c>RidePipelineQuestPdfIntegrationTests</c>, que tampoco la usa)
/// — se valida bytes no vacíos + cabecera <c>%PDF-</c> + que cada sección de composición
/// (<see cref="RetentionRideRenderingSectionsTests"/>) no lanza con los mismos datos, siguiendo el
/// mismo criterio ya establecido para Factura/Nota de Crédito.
/// </summary>
public sealed class QuestPdfRideRendererRetentionTests
{
    private static QuestPdfRideRenderer BuildRenderer() =>
        new(
            RideQrCodeGeneratorTestFactory.Create(),
            RideBarcodeGeneratorTestFactory.Create(),
            new NoOpFileStorage()
        );

    [Fact]
    public async Task RenderAsync_produces_a_non_empty_valid_pdf_for_a_full_retention_layout()
    {
        var layout = RetentionRideRenderingFixtures.Full();
        var renderer = BuildRenderer();

        var pdfBytes = await renderer.RenderAsync(layout, CancellationToken.None);

        pdfBytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(pdfBytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task RenderAsync_does_not_throw_when_authorization_is_still_pending()
    {
        var layout = RetentionRideRenderingFixtures.Minimal();
        layout.Header.AuthorizationDate.Should().BeNull();
        layout.AuthorizationDateDisplay.Should().Be("no disponible");
        var renderer = BuildRenderer();

        var act = async () => await renderer.RenderAsync(layout, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RenderAsync_produces_a_valid_pdf_for_the_minimal_retention_layout_too()
    {
        var layout = RetentionRideRenderingFixtures.Minimal();
        var renderer = BuildRenderer();

        var pdfBytes = await renderer.RenderAsync(layout, CancellationToken.None);

        pdfBytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(pdfBytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task RenderAsync_still_produces_a_valid_pdf_for_invoice_regression()
    {
        // Regresión explícita: soportar RetentionRideDocumentLayout no debe alterar en nada el
        // render de Factura, que ya existía antes de esta fase.
        var layout = RideRenderingFixtures.Full();
        var renderer = BuildRenderer();

        var pdfBytes = await renderer.RenderAsync(layout, CancellationToken.None);

        pdfBytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(pdfBytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task RenderAsync_still_throws_NotSupported_for_an_unknown_layout()
    {
        var renderer = BuildRenderer();

        var act = async () =>
            await renderer.RenderAsync(new UnknownLayout(), CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    private sealed class UnknownLayout : IRideDocumentLayout;
}
