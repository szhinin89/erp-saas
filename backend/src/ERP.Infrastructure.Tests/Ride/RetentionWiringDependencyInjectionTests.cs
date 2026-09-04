using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Modules.Ride.Branding;
using ERP.Application.Modules.Ride.Parsers;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Application.Modules.Ride.Services;
using ERP.Application.Modules.Ride.Templates;
using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// RETENTIONS-ELECTRONIC-WIRING-03E — construye un <see cref="ServiceProvider"/> real con el
/// wiring de Retención descrito en el ADR de esta fase (dos servicios pequeños, sin pasar por
/// <c>RidePipeline</c>/<c>IElectronicDocumentXmlBuilderResolver</c>) y confirma que resuelven sin
/// excepción. Las dependencias de más bajo nivel (repositorios de Retentions, renderer QuestPDF,
/// branding provider real) se mockean deliberadamente — este test valida el WIRING, no vuelve a
/// probar la lógica interna de cada pieza (ya cubierta por sus propios tests unitarios).
/// </summary>
public sealed class RetentionWiringDependencyInjectionTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        // Dependencias de más bajo nivel de IRetentionElectronicDocumentXmlService — mockeadas:
        // este test valida el wiring de los dos servicios de la fase 03E, no la lógica del data
        // provider (ya cubierta en RETENTIONS-ELECTRONIC-DOCUMENT-MODEL-03A) ni del XML builder
        // (RETENTIONS-SRI-XML-MAPPER-03B).
        var dataProviderMock = new Mock<IRetentionElectronicDocumentDataProvider>();
        services.AddScoped(_ => dataProviderMock.Object);
        var xmlBuilderMock = new Mock<IRetentionXmlBuilder>();
        services.AddScoped(_ => xmlBuilderMock.Object);
        services.AddScoped<
            IRetentionElectronicDocumentXmlService,
            RetentionElectronicDocumentXmlService
        >();

        // IRetentionRideXmlParser/IRetentionRideTemplate son reales (sin I/O, sin dependencias) —
        // mismo criterio que el resto del módulo Ride. IRideRenderer/IRideBrandingProvider se
        // mockean: sus implementaciones reales (QuestPdfRideRenderer, CompanyBrandingRideProvider)
        // ya tienen su propio wiring probado en RideDependencyInjectionTests.
        services.AddScoped<IRetentionRideXmlParser, RetentionRideXmlParser>();
        services.AddScoped<IRetentionRideTemplate, RetentionRideTemplate>();
        var rendererMock = new Mock<IRideRenderer>();
        rendererMock
            .Setup(r => r.RenderAsync(It.IsAny<IRideDocumentLayout>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);
        services.AddScoped(_ => rendererMock.Object);
        var brandingProviderMock = new Mock<IRideBrandingProvider>();
        brandingProviderMock
            .Setup(b =>
                b.GetAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<RideBranding>.Success(RideBranding.Empty()));
        services.AddScoped(_ => brandingProviderMock.Object);
        services.AddScoped<IRetentionRidePdfService, RetentionRidePdfService>();

        // ── Regresión: el resolver comercial de Factura/Nota de Crédito no cambia en esta fase ──
        services.AddScoped<IRideXmlParserResolver, RideXmlParserResolver>();
        services.AddScoped<IRideXmlParser, InvoiceRideXmlParser>();
        services.AddScoped<IRideXmlParser, CreditNoteRideXmlParser>();
        services.AddScoped<IRideTemplateResolver, RideTemplateResolver>();
        services.AddScoped<IRideTemplate, DefaultInvoiceRideTemplate>();
        services.AddScoped<IRideTemplate, CreditNoteRideTemplate>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void All_retention_wiring_services_resolve_without_error()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var act = () =>
        {
            sp.GetRequiredService<IRetentionElectronicDocumentXmlService>();
            sp.GetRequiredService<IRetentionRidePdfService>();
            sp.GetRequiredService<IRetentionRideXmlParser>();
            sp.GetRequiredService<IRetentionRideTemplate>();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Invoice_and_creditnote_still_resolve_through_the_commercial_resolvers()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var xmlParserResolver = sp.GetRequiredService<IRideXmlParserResolver>();
        xmlParserResolver
            .Resolve(ERP.Domain.Modules.Ride.Enums.RideDocumentType.Invoice)
            .Should()
            .BeOfType<InvoiceRideXmlParser>();
        xmlParserResolver
            .Resolve(ERP.Domain.Modules.Ride.Enums.RideDocumentType.CreditNote)
            .Should()
            .BeOfType<CreditNoteRideXmlParser>();

        var templateResolver = sp.GetRequiredService<IRideTemplateResolver>();
        templateResolver
            .Resolve(
                new RideTemplateSelector(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    null,
                    ERP.Domain.Modules.Ride.Enums.RideDocumentType.Invoice
                )
            )
            .Should()
            .BeOfType<DefaultInvoiceRideTemplate>();
    }

    [Fact]
    public void The_commercial_resolver_never_resolves_retention_the_fork_is_deliberate()
    {
        // RETENTIONS-RIDE-TEMPLATE-03C/03E: RetentionRideXmlParser/RetentionRideTemplate NUNCA se
        // registran como IRideXmlParser/IRideTemplate — el resolver comercial no los conoce.
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<IRideXmlParserResolver>()
            .Resolve(ERP.Domain.Modules.Ride.Enums.RideDocumentType.Retention)
            .Should()
            .BeNull("RetentionRideXmlParser no implementa IRideXmlParser — el fork es deliberado");

        // Solo Invoice/CreditNote están registrados como IRideXmlParser/IRideTemplate en este
        // proveedor (mismo bloque "Ride BC" real) — RetentionRideXmlParser/RetentionRideTemplate
        // no implementan esas interfaces, así que no podrían aparecer aquí aunque se registraran.
        sp.GetServices<IRideXmlParser>().Should().HaveCount(2);
        sp.GetServices<IRideTemplate>().Should().HaveCount(2);
    }
}
