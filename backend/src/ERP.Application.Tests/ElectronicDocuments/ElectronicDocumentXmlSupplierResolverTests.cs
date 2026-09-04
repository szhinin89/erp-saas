using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// RETENTIONS-SRI-AUTHORIZATION-WIRING-04D — <see cref="ElectronicDocumentXmlSupplierResolver"/>:
/// regla "explícito &gt; fallback comercial" (RETENTIONS-SRI-AUTHORIZATION-WIRING-DESIGN-04B,
/// sección E). Retención resuelve por supplier explícito; Factura/Nota de Crédito resuelven por
/// el fallback comercial instanciado al vuelo — nunca duplicando su registro.
/// </summary>
public sealed class ElectronicDocumentXmlSupplierResolverTests
{
    [Fact]
    public void Resolve_returns_the_explicit_supplier_for_retention()
    {
        var explicitSupplier = new Mock<IElectronicDocumentXmlSupplier>();
        explicitSupplier.Setup(s => s.DocumentType).Returns(ElectronicDocumentType.Retention);

        var dataProviderResolver = new Mock<IElectronicDocumentDataProviderResolver>();
        var xmlBuilderResolver = new Mock<IElectronicDocumentXmlBuilderResolver>();

        var resolver = new ElectronicDocumentXmlSupplierResolver(
            [explicitSupplier.Object],
            dataProviderResolver.Object,
            xmlBuilderResolver.Object
        );

        var result = resolver.Resolve(ElectronicDocumentType.Retention);

        result.Should().BeSameAs(explicitSupplier.Object);
        // El fallback comercial nunca se consulta cuando hay un supplier explícito registrado.
        dataProviderResolver.Verify(
            r => r.Resolve(It.IsAny<ElectronicDocumentType>()),
            Times.Never
        );
        xmlBuilderResolver.Verify(r => r.Resolve(It.IsAny<ElectronicDocumentType>()), Times.Never);
    }

    [Fact]
    public void Resolve_falls_back_to_the_commercial_path_for_invoice_and_creditnote()
    {
        var dataProvider = new Mock<IElectronicDocumentDataProvider>();
        var xmlBuilder = new Mock<IElectronicDocumentXmlBuilder>();

        var dataProviderResolver = new Mock<IElectronicDocumentDataProviderResolver>();
        dataProviderResolver
            .Setup(r => r.Resolve(ElectronicDocumentType.Invoice))
            .Returns(dataProvider.Object);
        dataProviderResolver
            .Setup(r => r.Resolve(ElectronicDocumentType.CreditNote))
            .Returns(dataProvider.Object);

        var xmlBuilderResolver = new Mock<IElectronicDocumentXmlBuilderResolver>();
        xmlBuilderResolver
            .Setup(r => r.Resolve(ElectronicDocumentType.Invoice))
            .Returns(xmlBuilder.Object);
        xmlBuilderResolver
            .Setup(r => r.Resolve(ElectronicDocumentType.CreditNote))
            .Returns(xmlBuilder.Object);

        var resolver = new ElectronicDocumentXmlSupplierResolver(
            [],
            dataProviderResolver.Object,
            xmlBuilderResolver.Object
        );

        var invoiceSupplier = resolver.Resolve(ElectronicDocumentType.Invoice);
        var creditNoteSupplier = resolver.Resolve(ElectronicDocumentType.CreditNote);

        invoiceSupplier.Should().BeOfType<CommercialElectronicDocumentXmlSupplier>();
        invoiceSupplier!.DocumentType.Should().Be(ElectronicDocumentType.Invoice);
        creditNoteSupplier.Should().BeOfType<CommercialElectronicDocumentXmlSupplier>();
        creditNoteSupplier!.DocumentType.Should().Be(ElectronicDocumentType.CreditNote);
    }

    [Fact]
    public void Resolve_returns_null_when_neither_an_explicit_supplier_nor_the_commercial_path_exists()
    {
        var dataProviderResolver = new Mock<IElectronicDocumentDataProviderResolver>();
        dataProviderResolver
            .Setup(r => r.Resolve(It.IsAny<ElectronicDocumentType>()))
            .Returns((IElectronicDocumentDataProvider?)null);
        var xmlBuilderResolver = new Mock<IElectronicDocumentXmlBuilderResolver>();
        xmlBuilderResolver
            .Setup(r => r.Resolve(It.IsAny<ElectronicDocumentType>()))
            .Returns((IElectronicDocumentXmlBuilder?)null);

        var resolver = new ElectronicDocumentXmlSupplierResolver(
            [],
            dataProviderResolver.Object,
            xmlBuilderResolver.Object
        );

        var result = resolver.Resolve(ElectronicDocumentType.DebitNote);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_returns_null_when_only_the_data_provider_side_of_the_commercial_path_exists()
    {
        // Fallo claro: el fallback comercial exige AMBOS lados (provider Y builder) — uno solo
        // no es suficiente para producir un ElectronicDocumentXml.
        var dataProvider = new Mock<IElectronicDocumentDataProvider>();
        var dataProviderResolver = new Mock<IElectronicDocumentDataProviderResolver>();
        dataProviderResolver
            .Setup(r => r.Resolve(ElectronicDocumentType.ShippingGuide))
            .Returns(dataProvider.Object);
        var xmlBuilderResolver = new Mock<IElectronicDocumentXmlBuilderResolver>();
        xmlBuilderResolver
            .Setup(r => r.Resolve(ElectronicDocumentType.ShippingGuide))
            .Returns((IElectronicDocumentXmlBuilder?)null);

        var resolver = new ElectronicDocumentXmlSupplierResolver(
            [],
            dataProviderResolver.Object,
            xmlBuilderResolver.Object
        );

        var result = resolver.Resolve(ElectronicDocumentType.ShippingGuide);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_never_registers_invoice_or_creditnote_as_an_explicit_supplier()
    {
        // Regresión de diseño: el resolver no debe necesitar (ni recibir) un supplier explícito
        // de Invoice/CreditNote para resolverlos — solo Retención se registra explícitamente.
        var retentionSupplier = new Mock<IElectronicDocumentXmlSupplier>();
        retentionSupplier.Setup(s => s.DocumentType).Returns(ElectronicDocumentType.Retention);

        var dataProvider = new Mock<IElectronicDocumentDataProvider>();
        var xmlBuilder = new Mock<IElectronicDocumentXmlBuilder>();
        var dataProviderResolver = new Mock<IElectronicDocumentDataProviderResolver>();
        dataProviderResolver
            .Setup(r => r.Resolve(ElectronicDocumentType.Invoice))
            .Returns(dataProvider.Object);
        var xmlBuilderResolver = new Mock<IElectronicDocumentXmlBuilderResolver>();
        xmlBuilderResolver
            .Setup(r => r.Resolve(ElectronicDocumentType.Invoice))
            .Returns(xmlBuilder.Object);

        var resolver = new ElectronicDocumentXmlSupplierResolver(
            [retentionSupplier.Object],
            dataProviderResolver.Object,
            xmlBuilderResolver.Object
        );

        resolver.Resolve(ElectronicDocumentType.Invoice).Should().BeOfType<CommercialElectronicDocumentXmlSupplier>();
    }
}
