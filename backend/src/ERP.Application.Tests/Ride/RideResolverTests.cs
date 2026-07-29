using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Ride;

/// <summary>
/// Sin ningún parser/plantilla registrado todavía (llegan en la Fase 6), ambos resolvers deben
/// comportarse igual que <c>ElectronicDocumentXmlBuilderResolver</c> con un diccionario vacío:
/// devolver <see langword="null"/> para cualquier tipo, nunca lanzar.
/// </summary>
public sealed class RideResolverTests
{
    [Theory]
    [InlineData(RideDocumentType.Invoice)]
    [InlineData(RideDocumentType.CreditNote)]
    [InlineData(RideDocumentType.PurchaseSettlement)]
    public void RideXmlParserResolver_with_no_registered_parsers_returns_null(
        RideDocumentType documentType
    )
    {
        var resolver = new RideXmlParserResolver([]);

        var result = resolver.Resolve(documentType);

        result.Should().BeNull();
    }

    [Fact]
    public void RideXmlParserResolver_with_no_registered_parsers_never_throws()
    {
        var resolver = new RideXmlParserResolver([]);

        var act = () => resolver.Resolve(RideDocumentType.Invoice);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(RideDocumentType.Invoice)]
    [InlineData(RideDocumentType.ShippingGuide)]
    public void RideTemplateResolver_with_no_registered_templates_returns_null(
        RideDocumentType documentType
    )
    {
        var resolver = new RideTemplateResolver([]);
        var selector = new RideTemplateSelector(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            documentType
        );

        var result = resolver.Resolve(selector);

        result.Should().BeNull();
    }

    [Fact]
    public void RideTemplateResolver_with_no_registered_templates_never_throws()
    {
        var resolver = new RideTemplateResolver([]);
        var selector = new RideTemplateSelector(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            RideDocumentType.Invoice
        );

        var act = () => resolver.Resolve(selector);

        act.Should().NotThrow();
    }
}
