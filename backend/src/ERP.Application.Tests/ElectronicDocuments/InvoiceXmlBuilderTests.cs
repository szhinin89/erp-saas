using System.Xml.Linq;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.ElectronicDocuments;

public sealed class InvoiceXmlBuilderTests
{
    /// <summary>Doble de prueba independiente de la implementación real registrada en DI
    /// (<c>SriTaxCategoryCodeResolver</c>, Fase 9) — mantiene estas pruebas desacopladas de
    /// esa clase concreta, ejercitando el builder contra cualquier resolver conforme al contrato.</summary>
    private sealed class FakeTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
    {
        public string? Resolve(string taxCode) => taxCode switch
        {
            "VAT" => "2",
            "ICE" => "3",
            _ => null,
        };
    }

    private sealed class NullTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
    {
        public string? Resolve(string taxCode) => null;
    }

    private static ElectronicDocumentData ValidInvoiceData() => new(
        Emission: new ElectronicDocumentEmissionContext(
            Environment: "1",
            EmissionType: "1",
            DocTypeCode: "01",
            Establishment: "001",
            EstablishmentAddress: "Av. Amazonas y Naciones Unidas",
            EmissionPoint: "001",
            Sequential: "000000123",
            IssueDate: new DateTime(2026, 7, 8)),
        Issuer: new ElectronicDocumentIssuerData(
            TaxId: "1790012345001",
            LegalName: "ACME CIA LTDA",
            TradeName: "ACME",
            MatrixAddress: "Av. Amazonas y Naciones Unidas",
            TaxRegime: null,
            IsAccountingRequired: true),
        Counterparty: new ElectronicDocumentCounterpartyData(
            IdentificationType: "05",
            IdentificationNumber: "1710034065",
            LegalName: "Juan Perez",
            Address: "Calle Falsa 123",
            Email: "juan@example.com"),
        Details:
        [
            new ElectronicDocumentDetailLine(
                Code: "SKU-001",
                Description: "Producto de prueba",
                Quantity: 2m,
                UnitPrice: 10m,
                Discount: 0m,
                Subtotal: 20m,
                Taxes: [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)]),
        ],
        TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 20m, 3m)],
        Totals: new ElectronicDocumentTotals(
            Subtotal: 20m, TotalDiscount: 0m, TotalTax: 3m, GrandTotal: 23m, CurrencyCode: "USD"),
        Payments: [new ElectronicDocumentPayment("01", 23m, null, null)],
        AdditionalInfo: []);

    [Fact]
    public void Build_valid_invoice_returns_wellformed_xml_with_49_digit_access_key()
    {
        var builder = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver());

        var result = builder.Build(ValidInvoiceData());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.AccessKey.Should().HaveLength(49);
        result.Value.AccessKey.Should().MatchRegex("^[0-9]{49}$");
        result.Value.DocumentType.Should().Be(ElectronicDocumentType.Invoice);

        var xdoc = XDocument.Parse(result.Value.Xml);
        xdoc.Root!.Name.LocalName.Should().Be("factura");
        xdoc.Root.Element("infoTributaria")!.Element("claveAcceso")!.Value.Should().Be(result.Value.AccessKey);
        xdoc.Root.Element("infoTributaria")!.Element("ruc")!.Value.Should().Be("1790012345001");
        xdoc.Root.Element("infoTributaria")!.Element("codDoc")!.Value.Should().Be("01");
        xdoc.Root.Element("infoFactura")!.Element("importeTotal")!.Value.Should().Be("23.00");
        xdoc.Root.Element("detalles")!.Elements("detalle").Should().HaveCount(1);
        xdoc.Root.Element("detalles")!.Element("detalle")!.Element("impuestos")!
            .Element("impuesto")!.Element("codigo")!.Value.Should().Be("2");
    }

    [Fact]
    public void Build_declares_utf8_encoding_consistent_with_how_it_is_persisted()
    {
        // XML-01 (auditoría SRI, Fase 2): StringWriter.Save() por defecto declaraba
        // encoding="utf-16" en el XML aunque ElectronicDocumentXmlStorageService lo persiste
        // como bytes UTF-8 (Encoding.UTF8.GetBytes) — una declaración inconsistente con los
        // bytes reales del archivo. XDocument.Parse(string) NO detecta este defecto (un string
        // en memoria no tiene bytes que contradecir), por eso se verifica el texto literal de la
        // declaración y, además, que el string decodifique sin excepción al reinterpretarse como
        // el stream de bytes UTF-8 con el que realmente se persiste.
        var builder = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver());

        var result = builder.Build(ValidInvoiceData());

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>",
            "la declaración debe coincidir con los bytes reales con los que se persiste el XML (UTF-8)");
        result.Value.Xml.Should().NotContain("utf-16",
            "el XML nunca debe declararse en UTF-16 — se persiste siempre como bytes UTF-8");

        var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(result.Value.Xml);
        var act = () =>
        {
            using var stream = new MemoryStream(utf8Bytes);
            using var reader = System.Xml.XmlReader.Create(stream);
            while (reader.Read()) { }
        };
        act.Should().NotThrow("un lector que respete la declaración del XML no debe fallar al parsear los bytes reales con los que se persiste");
    }

    [Fact]
    public void Build_access_key_check_digit_is_deterministic_mod11()
    {
        var builder = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver());

        var first = builder.Build(ValidInvoiceData());
        var second = builder.Build(ValidInvoiceData());

        first.Value!.AccessKey.Should().Be(second.Value!.AccessKey);
    }

    [Fact]
    public void Build_without_totals_fails_with_clear_message_not_exception()
    {
        var data = ValidInvoiceData() with { Totals = null };
        var builder = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver());

        var result = builder.Build(data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("totales");
    }

    [Fact]
    public void Build_without_details_fails_with_clear_message()
    {
        var data = ValidInvoiceData() with { Details = [] };
        var builder = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver());

        var result = builder.Build(data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("detalle");
    }

    /// <summary>
    /// XML-02 (auditoría SRI, re-auditoría independiente): la ficha técnica limita
    /// &lt;infoAdicional&gt; a un máximo de 15 &lt;campoAdicional&gt;. Antes de este fix el
    /// builder serializaba cualquier cantidad sin validar, generando un XML que el XSD del SRI
    /// rechazaría.
    /// </summary>
    [Fact]
    public void Build_with_more_than_15_additional_fields_fails_with_clear_message()
    {
        var data = ValidInvoiceData() with
        {
            AdditionalInfo = Enumerable.Range(1, 16)
                .Select(i => new ElectronicDocumentAdditionalField($"Campo{i}", "valor"))
                .ToList(),
        };
        var builder = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver());

        var result = builder.Build(data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("15");
    }

    [Fact]
    public void Build_with_exactly_15_additional_fields_succeeds()
    {
        var data = ValidInvoiceData() with
        {
            AdditionalInfo = Enumerable.Range(1, 15)
                .Select(i => new ElectronicDocumentAdditionalField($"Campo{i}", "valor"))
                .ToList(),
        };
        var builder = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver());

        var result = builder.Build(data);

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public void Build_without_doc_type_code_fails_with_clear_message()
    {
        var data = ValidInvoiceData() with
        {
            Emission = ValidInvoiceData().Emission with { DocTypeCode = "" },
        };
        var builder = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver());

        var result = builder.Build(data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("tipo de comprobante");
    }

    /// <summary>
    /// Documenta el comportamiento de resguardo si algún día se registrara un resolver que no
    /// reconoce "VAT"/"ICE" (p.ej. un tercer tipo de impuesto todavía no mapeado): el builder
    /// debe fallar de forma controlada, nunca inventar "2"/"3" hardcodeados ni lanzar una
    /// excepción sin capturar.
    /// </summary>
    [Fact]
    public void Build_without_tax_category_catalog_fails_with_clear_message_not_exception()
    {
        var builder = new InvoiceXmlBuilder(new NullTaxCategoryCodeResolver());

        var result = builder.Build(ValidInvoiceData());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("código de impuesto");
        result.Error.Should().Contain("VAT");
    }
}
