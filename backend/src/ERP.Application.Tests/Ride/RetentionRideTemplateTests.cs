using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;

namespace ERP.Application.Tests.Ride;

/// <summary>RETENTIONS-RIDE-TEMPLATE-03C — <see cref="RetentionRideTemplate"/>.</summary>
public sealed class RetentionRideTemplateTests
{
    private static RetentionRideModel ValidModel(DateTime? authorizationDate = null)
    {
        var accessKey = RideAccessKey.Create(new string('9', 49));
        var header = RetentionRideHeader.Create(
            environment: "1",
            emissionType: "1",
            documentTypeCode: "07",
            establishment: "001",
            emissionPoint: "001",
            sequential: "000000001",
            establishmentAddress: "Av. Principal 123",
            issueDate: new DateOnly(2026, 8, 5),
            fiscalPeriod: "08/2026",
            accessKey: accessKey,
            authorizationNumber: accessKey.Value,
            authorizationDate: authorizationDate
        );
        var issuer = RideParty.Create(null, "1790012345001", "Empresa Test S.A.", "Empresa Test", "Matriz 456");
        var subjectWithheld = RideParty.Create("05", "1710034065", "Proveedor Test");
        var sourceDocument = RetentionRideSourceDocument.Create(
            "01",
            "001001000000456",
            new DateOnly(2026, 8, 1)
        );
        var lines = new[]
        {
            RetentionRideTaxLine.Create("2", "725", 15m, 30m, 4.5m),
            RetentionRideTaxLine.Create("1", "303", 100m, 8m, 8m),
        };

        return RetentionRideModel.Create(
            header,
            issuer,
            subjectWithheld,
            sourceDocument,
            lines,
            totalRetained: 12.5m,
            additionalInfo: []
        );
    }

    [Fact]
    public void DocumentType_is_Retention()
    {
        new RetentionRideTemplate().DocumentType.Should().Be(RideDocumentType.Retention);
    }

    [Fact]
    public void Compose_produces_a_RetentionRideDocumentLayout_carrying_every_field()
    {
        var model = ValidModel(authorizationDate: new DateTime(2026, 8, 5, 10, 30, 0));

        var layout = new RetentionRideTemplate().Compose(model, RideBranding.Empty());

        layout.Should().BeOfType<RetentionRideDocumentLayout>();
        var retentionLayout = (RetentionRideDocumentLayout)layout;
        retentionLayout.Header.Should().Be(model.Header);
        retentionLayout.Issuer.LegalName.Should().Be("Empresa Test S.A.");
        retentionLayout.SubjectWithheld.LegalName.Should().Be("Proveedor Test");
        retentionLayout.SourceDocument.Number.Should().Be("001001000000456");
        retentionLayout.Lines.Should().HaveCount(2);
        retentionLayout.TotalRetained.Should().Be(12.5m);
        retentionLayout.QrPlaceholder.Should().Be(model.Header.AccessKey.Value);
        retentionLayout.AuthorizationDateDisplay.Should().Be("05/08/2026 10:30:00");
    }

    [Fact]
    public void Compose_does_not_throw_and_shows_a_safe_fallback_when_not_yet_authorized()
    {
        // AuthorizationDate == null: el comprobante todavía no fue autorizado por el SRI. El
        // template nunca debe inventar una fecha ni romper — debe componer el layout con el mismo
        // fallback ya usado por HeaderSection para Factura/Nota de Crédito.
        var model = ValidModel(authorizationDate: null);

        var act = () => new RetentionRideTemplate().Compose(model, RideBranding.Empty());

        act.Should().NotThrow();
        var layout = (RetentionRideDocumentLayout)act();
        layout.Header.AuthorizationDate.Should().BeNull();
        layout.AuthorizationDateDisplay.Should().Be("no disponible");
        // El número de autorización SÍ está disponible (regla AUTH-01: es la clave de acceso) aun
        // sin fecha de autorización — nunca queda vacío ni inventado.
        layout.Header.AuthorizationNumber.Should().Be(layout.Header.AccessKey.Value);
    }

    [Fact]
    public void RideXmlParserResolver_does_not_resolve_retention_the_generic_parser_fork_is_deliberate()
    {
        // Documenta la decisión de fork: RetentionRideXmlParser NO implementa IRideXmlParser, así
        // que el resolver genérico nunca lo encuentra — es responsabilidad exclusiva de
        // IRetentionRideXmlParser, resuelto aparte. Esto no es un gap accidental.
        var resolver = new RideXmlParserResolver([new InvoiceRideXmlParser(), new CreditNoteRideXmlParser()]);

        var parser = resolver.Resolve(RideDocumentType.Retention);

        parser.Should().BeNull();
    }

    [Fact]
    public void RideXmlParserResolver_still_resolves_invoice_and_creditnote_regression()
    {
        var resolver = new RideXmlParserResolver([new InvoiceRideXmlParser(), new CreditNoteRideXmlParser()]);

        resolver.Resolve(RideDocumentType.Invoice).Should().BeOfType<InvoiceRideXmlParser>();
        resolver.Resolve(RideDocumentType.CreditNote).Should().BeOfType<CreditNoteRideXmlParser>();
    }
}
