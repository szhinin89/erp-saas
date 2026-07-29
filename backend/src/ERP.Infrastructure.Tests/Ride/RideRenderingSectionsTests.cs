using ERP.Application.Modules.Ride.Templates;
using ERP.Infrastructure.Ride.Rendering.Sections;
using FluentAssertions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// Cada sección se prueba de forma aislada: consume únicamente el layout, nunca lanza, admite
/// datos opcionales ausentes y colecciones vacías.
/// </summary>
public sealed class RideRenderingSectionsTests
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

    private static byte[] CreateBarcodeBytes(InvoiceRideDocumentLayout layout) =>
        RideBarcodeGeneratorTestFactory.Create().Generate(layout.Header.AccessKey);

    private static byte[] CreateQrBytes(InvoiceRideDocumentLayout layout) =>
        RideQrCodeGeneratorTestFactory.Create().Generate(layout.Header.AccessKey);

    [Fact]
    public void HeaderSection_renders_full_layout_without_throwing()
    {
        var layout = RideRenderingFixtures.Full();
        var barcodeBytes = CreateBarcodeBytes(layout);

        var act = () =>
            RenderInIsolation(c => HeaderSection.Compose(c, layout, logoBytes: null, barcodeBytes));

        act.Should().NotThrow();
    }

    [Fact]
    public void HeaderSection_handles_missing_authorization_date()
    {
        var layout = RideRenderingFixtures.Minimal();
        layout.Header.AuthorizationDate.Should().BeNull();
        var barcodeBytes = CreateBarcodeBytes(layout);

        var act = () =>
            RenderInIsolation(c => HeaderSection.Compose(c, layout, logoBytes: null, barcodeBytes));

        act.Should().NotThrow();
    }

    [Fact]
    public void HeaderSection_renders_a_real_logo_without_throwing()
    {
        var layout = RideRenderingFixtures.Full();
        var logoBytes = CreateQrBytes(layout);
        var barcodeBytes = CreateBarcodeBytes(layout);

        var act = () =>
            RenderInIsolation(c => HeaderSection.Compose(c, layout, logoBytes, barcodeBytes));

        act.Should().NotThrow();
    }

    [Fact]
    public void BuyerSection_handles_optional_address_absent()
    {
        var layout = RideRenderingFixtures.Minimal();
        layout.Receiver.Address.Should().BeNull();

        var act = () => RenderInIsolation(c => BuyerSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void BuyerSection_renders_full_party_data_without_throwing()
    {
        var layout = RideRenderingFixtures.Full();

        var act = () => RenderInIsolation(c => BuyerSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void LinesSection_renders_multiple_lines_without_throwing()
    {
        var layout = RideRenderingFixtures.ManyLines(8);

        var act = () => RenderInIsolation(c => LinesSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void LinesSection_handles_a_single_line()
    {
        var layout = RideRenderingFixtures.Minimal();
        layout.Lines.Should().ContainSingle();

        var act = () => RenderInIsolation(c => LinesSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void TaxSummarySection_renders_multiple_tax_codes_without_throwing()
    {
        var layout = RideRenderingFixtures.Full();
        layout.TaxSummary.Should().HaveCount(2);

        var act = () => RenderInIsolation(c => TaxSummarySection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void PaymentsSection_renders_multiple_payments_with_term_and_time_unit()
    {
        var layout = RideRenderingFixtures.Full();
        layout.Payments.Should().Contain(p => p.Term != null);

        var act = () => RenderInIsolation(c => PaymentsSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void PaymentsSection_handles_a_single_payment_without_term()
    {
        var layout = RideRenderingFixtures.Minimal();

        var act = () => RenderInIsolation(c => PaymentsSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void AdditionalInfoSection_handles_an_empty_collection()
    {
        var layout = RideRenderingFixtures.Minimal();
        layout.AdditionalInfo.Should().BeEmpty();

        var act = () => RenderInIsolation(c => AdditionalInfoSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void AdditionalInfoSection_renders_populated_fields_without_throwing()
    {
        var layout = RideRenderingFixtures.Full();
        layout.AdditionalInfo.Should().HaveCount(2);

        var act = () => RenderInIsolation(c => AdditionalInfoSection.Compose(c, layout));

        act.Should().NotThrow();
    }

    [Fact]
    public void QrFooterSection_renders_the_access_key_placeholder_without_throwing()
    {
        var layout = RideRenderingFixtures.Minimal();
        var qrImageBytes = CreateQrBytes(layout);

        var act = () => RenderInIsolation(c => QrFooterSection.Compose(c, layout, qrImageBytes));

        act.Should().NotThrow();
    }

    [Fact]
    public void QrFooterSection_handles_absent_footer_text_in_branding()
    {
        var layout = RideRenderingFixtures.Minimal();
        layout.Branding.FooterText.Should().BeNull();
        var qrImageBytes = CreateQrBytes(layout);

        var act = () => RenderInIsolation(c => QrFooterSection.Compose(c, layout, qrImageBytes));

        act.Should().NotThrow();
    }
}
