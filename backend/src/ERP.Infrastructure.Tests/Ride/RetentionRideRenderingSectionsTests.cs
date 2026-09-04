using ERP.Infrastructure.Ride.Rendering.Sections;
using FluentAssertions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// RETENTIONS-RIDE-PDF-RENDERER-03D — cada sección de Retención se prueba de forma aislada,
/// mismo criterio que <see cref="RideRenderingSectionsTests"/> (Factura): consume únicamente el
/// layout, nunca lanza, admite datos opcionales ausentes (documento sustento, nombre comercial).
/// </summary>
public sealed class RetentionRideRenderingSectionsTests
{
    private static byte[] RenderInIsolation(Action<IContainer> compose) =>
        Document
            .Create(container =>
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.Content().Element(compose);
                })
            )
            .GeneratePdf();

    private static byte[] CreateBarcodeBytes() =>
        RideBarcodeGeneratorTestFactory.Create().Generate(RetentionRideRenderingFixtures.Full().Header.AccessKey);

    private static byte[] CreateQrBytes() =>
        RideQrCodeGeneratorTestFactory.Create().Generate(RetentionRideRenderingFixtures.Full().Header.AccessKey);

    [Fact]
    public void RetentionHeaderSection_renders_full_layout_without_throwing()
    {
        var layout = RetentionRideRenderingFixtures.Full();
        var barcodeBytes = CreateBarcodeBytes();

        var act = () =>
            RenderInIsolation(c =>
                RetentionHeaderSection.Compose(c, layout, logoBytes: null, barcodeBytes)
            );

        act.Should().NotThrow();
    }

    [Fact]
    public void RetentionHeaderSection_handles_missing_authorization_date_with_the_layout_fallback()
    {
        var layout = RetentionRideRenderingFixtures.Minimal();
        layout.Header.AuthorizationDate.Should().BeNull();
        layout.AuthorizationDateDisplay.Should().Be("no disponible");
        var barcodeBytes = CreateBarcodeBytes();

        var act = () =>
            RenderInIsolation(c =>
                RetentionHeaderSection.Compose(c, layout, logoBytes: null, barcodeBytes)
            );

        act.Should().NotThrow();
    }

    [Fact]
    public void RetentionSubjectSection_renders_the_withheld_party_and_fiscal_period()
    {
        var layout = RetentionRideRenderingFixtures.Full();

        var act = () => RenderInIsolation(c => RetentionSubjectSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void RetentionSourceDocumentSection_handles_a_fully_absent_source_document()
    {
        var layout = RetentionRideRenderingFixtures.Minimal();
        layout.SourceDocument.DocumentTypeCode.Should().BeNull();
        layout.SourceDocument.Number.Should().BeNull();
        layout.SourceDocument.IssueDate.Should().BeNull();

        var act = () => RenderInIsolation(c => RetentionSourceDocumentSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void RetentionSourceDocumentSection_renders_a_populated_source_document()
    {
        var layout = RetentionRideRenderingFixtures.Full();
        layout.SourceDocument.Number.Should().NotBeNull();

        var act = () => RenderInIsolation(c => RetentionSourceDocumentSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void RetentionTaxLinesSection_renders_vat_and_income_lines_without_throwing()
    {
        var layout = RetentionRideRenderingFixtures.Full();
        layout.Lines.Should().HaveCount(2);

        var act = () => RenderInIsolation(c => RetentionTaxLinesSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void RetentionTaxLinesSection_handles_a_single_line()
    {
        var layout = RetentionRideRenderingFixtures.Minimal();
        layout.Lines.Should().ContainSingle();

        var act = () => RenderInIsolation(c => RetentionTaxLinesSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void RetentionTotalSection_renders_the_total_retained_without_recalculating()
    {
        var layout = RetentionRideRenderingFixtures.Full();
        layout.TotalRetained.Should().Be(12.5m);

        var act = () => RenderInIsolation(c => RetentionTotalSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void AdditionalInfoSection_renders_the_retention_layout_overload_without_throwing()
    {
        var layout = RetentionRideRenderingFixtures.Full();
        layout.AdditionalInfo.Should().BeEmpty();

        var act = () => RenderInIsolation(c => AdditionalInfoSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void QrFooterSection_renders_the_retention_layout_overload_without_throwing()
    {
        var layout = RetentionRideRenderingFixtures.Full();
        var qrImageBytes = CreateQrBytes();

        var act = () => RenderInIsolation(c => QrFooterSection.Compose(c, layout, qrImageBytes));

        act.Should().NotThrow();
    }
}
