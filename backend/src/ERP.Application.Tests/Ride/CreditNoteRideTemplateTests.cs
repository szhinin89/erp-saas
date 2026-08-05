using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;

namespace ERP.Application.Tests.Ride;

/// <summary>ADR-031 addendum (Fase 12, P0-01) — <see cref="CreditNoteRideTemplate"/>.</summary>
public sealed class CreditNoteRideTemplateTests
{
    [Fact]
    public void RideTemplateResolver_resolves_CreditNoteRideTemplate_for_CreditNote()
    {
        var resolver = new RideTemplateResolver([
            new DefaultInvoiceRideTemplate(),
            new CreditNoteRideTemplate(),
        ]);

        var template = resolver.Resolve(
            new RideTemplateSelector(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                null,
                RideDocumentType.CreditNote
            )
        );

        template.Should().NotBeNull();
        template.Should().BeOfType<CreditNoteRideTemplate>();
        template!.DocumentType.Should().Be(RideDocumentType.CreditNote);
    }

    [Fact]
    public void RideTemplateResolver_still_resolves_DefaultInvoiceRideTemplate_for_Invoice()
    {
        // Regresión explícita: registrar CreditNoteRideTemplate no debe alterar en nada la
        // resolución de Factura, que ya existía antes de esta fase.
        var resolver = new RideTemplateResolver([
            new DefaultInvoiceRideTemplate(),
            new CreditNoteRideTemplate(),
        ]);

        var template = resolver.Resolve(
            new RideTemplateSelector(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                null,
                RideDocumentType.Invoice
            )
        );

        template.Should().NotBeNull();
        template.Should().BeOfType<DefaultInvoiceRideTemplate>();
    }

    [Fact]
    public void Compose_reuses_InvoiceRideDocumentLayout_carrying_reason_and_modified_document()
    {
        var header = RideHeader.Create(
            environment: "2",
            emissionType: "1",
            documentTypeCode: "04",
            establishment: "001",
            emissionPoint: "001",
            sequential: "000000001",
            establishmentAddress: "Av. Principal 123",
            issueDate: new DateOnly(2026, 7, 30),
            currencyCode: "USD",
            accessKey: RideAccessKey.Create(new string('9', 49)),
            authorizationNumber: new string('9', 49),
            authorizationDate: null,
            subtotalWithoutTax: 20m,
            totalDiscount: 0m,
            tip: 0m,
            grandTotal: 23m,
            reason: "Producto en mal estado",
            modifiedDocument: RideModifiedDocumentReference.Create(
                "01",
                "001-001-000000045",
                new DateOnly(2026, 7, 20)
            )
        );
        var issuer = RideParty.Create(null, "1790012345001", "Empresa Test S.A.");
        var receiver = RideParty.Create("05", "1710034065", "Cliente Test");
        var line = RideLine.Create(
            "SKU-001",
            "Producto devuelto",
            2m,
            10m,
            0m,
            20m,
            [RideTaxSummary.Create("2", "2", 20m, 3m, rate: 15m)]
        );
        var model = RideModel.Create(
            header,
            issuer,
            receiver,
            [line],
            [RideTaxSummary.Create("2", "2", 20m, 3m)],
            [],
            []
        );

        var layout = new CreditNoteRideTemplate().Compose(model, RideBranding.Empty());

        layout
            .Should()
            .BeOfType<InvoiceRideDocumentLayout>(
                "reutiliza el mismo layout/renderer de Factura, sin crear un pipeline PDF paralelo"
            );
        var invoiceLayout = (InvoiceRideDocumentLayout)layout;
        invoiceLayout.Header.Reason.Should().Be("Producto en mal estado");
        invoiceLayout.Header.ModifiedDocument.Should().NotBeNull();
        invoiceLayout.Header.ModifiedDocument!.DocumentTypeCode.Should().Be("01");
        invoiceLayout.Header.ModifiedDocument.Number.Should().Be("001-001-000000045");
        invoiceLayout.Lines.Should().ContainSingle();
        invoiceLayout.QrPlaceholder.Should().Be(header.AccessKey.Value);
    }
}
