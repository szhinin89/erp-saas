using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Infrastructure.Services.ElectronicDocuments;
using ERP.Infrastructure.Services.Sri;
using FluentAssertions;
using System.Xml;
using System.Xml.Linq;

namespace ERP.Infrastructure.Tests.Services.Sri;

/// <summary>
/// Fase 9, punto 6 — validación integral: genera una Factura real (XML generado con el
/// resolver tributario real, no un doble de prueba) y verifica que el flujo llega hasta "XML
/// generado → Firma correcta", sin enviar nada al SRI.
///
/// La validación contra el XSD oficial (paso intermedio del flujo real,
/// <c>InvoiceXmlSchemaValidator</c>/<c>ElectronicDocumentIssuer</c>) queda fuera de esta prueba:
/// el proyecto no tiene incorporado el <c>factura_v1.1.0.xsd</c> oficial del SRI (ver
/// <c>ERP.Infrastructure/ElectronicDocuments/Xsd/Invoice/README.md</c>, que prohíbe
/// expresamente colocar un XSD reconstruido/no verificable) — es un bloqueador documentado de
/// la Fase 9, no algo que esta prueba pueda ni deba simular.
///
/// Esta prueba NUNCA invoca <see cref="SriSoapClient.SendAsync"/> ni
/// <see cref="SriSoapClient.CheckAuthorizationAsync"/> — el envío real al SRI es
/// explícitamente una fase posterior.
/// </summary>
public sealed class ElectronicInvoicePipelineIntegrationTests
{
    private static ElectronicDocumentData ValidInvoiceData() =>
        new(
            Emission: new ElectronicDocumentEmissionContext(
                Environment: "1",
                EmissionType: "1",
                DocTypeCode: "01",
                Establishment: "001",
                EstablishmentAddress: "Av. Amazonas y Naciones Unidas",
                EmissionPoint: "001",
                Sequential: "000000123",
                IssueDate: new DateTime(2026, 7, 9)
            ),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001",
                LegalName: "ACME CIA LTDA",
                TradeName: "ACME",
                MatrixAddress: "Av. Amazonas y Naciones Unidas",
                TaxRegime: null,
                IsAccountingRequired: true
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: "05",
                IdentificationNumber: "1710034065",
                LegalName: "Juan Perez",
                Address: "Calle Falsa 123",
                Email: "juan@example.com"
            ),
            Details:
            [
                new ElectronicDocumentDetailLine(
                    Code: "SKU-001",
                    Description: "Producto de prueba",
                    Quantity: 2m,
                    UnitPrice: 10m,
                    Discount: 0m,
                    Subtotal: 20m,
                    Taxes: [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)]
                ),
            ],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 20m, 3m)],
            Totals: new ElectronicDocumentTotals(
                Subtotal: 20m,
                TotalDiscount: 0m,
                TotalTax: 3m,
                GrandTotal: 23m,
                CurrencyCode: "USD"
            ),
            Payments: [new ElectronicDocumentPayment("01", 23m, null, null)],
            AdditionalInfo: []
        );

    [Fact]
    public void Real_invoice_reaches_wellformed_xml_and_a_cryptographically_valid_XAdES_BES_signature()
    {
        // 1. XML generado — con el resolver tributario REAL registrado en producción (Fase 9),
        // no un doble de prueba: si el mapeo VAT→"2"/ICE→"3" se rompiera, esta prueba lo detectaría.
        var builder = new InvoiceXmlBuilder(new SriTaxCategoryCodeResolver());
        var buildResult = builder.Build(ValidInvoiceData());

        buildResult.IsSuccess.Should().BeTrue(buildResult.Error);
        var xml = buildResult.Value!.Xml;

        // Bien formado + namespace/estructura mínima esperada.
        var xdoc = XDocument.Parse(xml);
        xdoc.Root!.Name.LocalName.Should().Be("factura");
        xdoc.Root.Attribute("version")!.Value.Should().Be("1.1.0");
        xdoc.Root.Element("infoTributaria")!.Element("codDoc")!.Value.Should().Be("01");

        // 2. Firma XAdES-BES — usando un certificado de prueba autofirmado (nunca uno real del SRI).
        var p12Path = TestP12CertificateFactory.CreateTempP12File();
        try
        {
            var signedBytes = XadesBesSigner.Sign(xml, p12Path, TestP12CertificateFactory.Password);

            var signedDoc = new XmlDocument { PreserveWhitespace = true };
            using (var ms = new MemoryStream(signedBytes))
                signedDoc.Load(ms);

            // El documento firmado sigue siendo la Factura original, con la firma embebida.
            signedDoc.DocumentElement!.Name.Should().Be("factura");

            var nsMgr = new XmlNamespaceManager(signedDoc.NameTable);
            nsMgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
            var signatureNode = signedDoc.SelectSingleNode("//ds:Signature", nsMgr) as XmlElement;
            signatureNode.Should().NotBeNull();

            var signedXml = new XadesSignedXml(signedDoc);
            signedXml.LoadXml(signatureNode!);

            signedXml
                .CheckSignature()
                .Should()
                .BeTrue(
                    "una Factura Electrónica real debe poder generarse y firmarse con una firma "
                        + "XAdES-BES criptográficamente válida, sin depender de la validación XSD "
                        + "(bloqueada — ver README del directorio Xsd/Invoice) para demostrarlo"
                );
        }
        finally
        {
            File.Delete(p12Path);
        }
    }
}
