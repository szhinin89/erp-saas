using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;

namespace ERP.Application.Tests.Ride;

public sealed class DefaultInvoiceRideTemplateTests
{
    [Fact]
    public void DocumentType_is_Invoice()
    {
        new DefaultInvoiceRideTemplate().DocumentType.Should().Be(RideDocumentType.Invoice);
    }

    [Fact]
    public void Compose_produces_a_complete_InvoiceRideDocumentLayout()
    {
        var model = RideTestModelBuilder.Build();
        var branding = RideBranding.Create(
            logoStoragePath: "branding/logo.png",
            primaryColorHex: "#112233",
            secondaryColorHex: "#445566",
            footerText: "Gracias por su compra");
        var template = new DefaultInvoiceRideTemplate();

        var layout = template.Compose(model, branding);

        layout.Should().BeOfType<InvoiceRideDocumentLayout>();
        var invoiceLayout = (InvoiceRideDocumentLayout)layout;

        // Header
        invoiceLayout.Header.Should().Be(model.Header);
        invoiceLayout.Header.AccessKey.Value.Should().HaveLength(49);

        // Líneas
        invoiceLayout.Lines.Should().HaveCount(model.Lines.Count);
        invoiceLayout.Lines.Should().BeEquivalentTo(model.Lines);

        // Impuestos
        invoiceLayout.TaxSummary.Should().NotBeEmpty();
        invoiceLayout.TaxSummary.Should().BeEquivalentTo(model.TaxSummary);

        // Branding
        invoiceLayout.Branding.Should().Be(branding);
        invoiceLayout.Branding.PrimaryColorHex.Should().Be("#112233");

        // QR placeholder — dato crudo, nunca una imagen
        invoiceLayout.QrPlaceholder.Should().Be(model.Header.AccessKey.Value);
        invoiceLayout.QrPlaceholder.Should().MatchRegex("^[0-9]{49}$");
    }

    [Fact]
    public void Compose_never_produces_bytes_only_a_composition_model()
    {
        var model = RideTestModelBuilder.Build();
        var template = new DefaultInvoiceRideTemplate();

        var layout = template.Compose(model, RideBranding.Empty());

        // El tipo de retorno (IRideDocumentLayout, sin miembros propios) ya impide que Compose
        // exponga bytes en su firma — esta prueba solo confirma que produce un objeto real.
        layout.Should().NotBeNull();
        layout.Should().BeOfType<InvoiceRideDocumentLayout>();
    }
}
