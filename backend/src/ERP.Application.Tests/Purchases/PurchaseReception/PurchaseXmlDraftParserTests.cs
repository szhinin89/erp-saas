using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using FluentAssertions;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

/// <summary>
/// Prueba <see cref="PurchaseXmlDraftParser"/> contra XML real, no artesanal: los fixtures se
/// generan con <c>InvoiceXmlBuilder</c> (ElectronicDocuments, FROZEN) — la misma clase de
/// producción que emite el XML que un proveedor efectivamente autoriza — para garantizar que la
/// forma exacta coincide con lo que el SRI realmente entrega.
/// </summary>
public sealed class PurchaseXmlDraftParserTests
{
    private sealed class FakeTaxCategoryCodeResolver : ISriTaxCategoryCodeResolver
    {
        public string? Resolve(string taxCode) => taxCode switch
        {
            "VAT" => "2",
            "ICE" => "3",
            _ => null,
        };
    }

    private static string BuildAuthorizedXml(ElectronicDocumentData data)
    {
        var builder = new InvoiceXmlBuilder(new FakeTaxCategoryCodeResolver());
        var result = builder.Build(data);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value!.Xml;
    }

    [Fact]
    public void Parses_header_and_a_single_line_with_vat_only()
    {
        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                Environment: "1", EmissionType: "1", DocTypeCode: "01",
                Establishment: "001", EstablishmentAddress: "Av. Amazonas y Naciones Unidas",
                EmissionPoint: "001", Sequential: "000000123", IssueDate: new DateTime(2026, 7, 8)),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001", LegalName: "PROVEEDOR ACME S.A.", TradeName: "ACME",
                MatrixAddress: "Av. Amazonas y Naciones Unidas", TaxRegime: null, IsAccountingRequired: true),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: "05", IdentificationNumber: "1710034065",
                LegalName: "Empresa Compradora", Address: "Calle Falsa 123", Email: null),
            Details: [new ElectronicDocumentDetailLine(
                Code: "SKU-001", Description: "Producto de prueba", Quantity: 2m, UnitPrice: 10m,
                Discount: 0m, Subtotal: 20m,
                Taxes: [new ElectronicDocumentDetailTax("VAT", "2", 20m, 15m, 3m)])],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "2", 20m, 3m)],
            Totals: new ElectronicDocumentTotals(20m, 0m, 3m, 23m, "USD"),
            Payments: [new ElectronicDocumentPayment("01", 23m, null, null)],
            AdditionalInfo: []);

        var xml = BuildAuthorizedXml(data);
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var draft = result.Value!;

        draft.SupplierRuc.Should().Be("1790012345001");
        draft.SupplierName.Should().Be("PROVEEDOR ACME S.A.");
        draft.DocTypeCode.Should().Be("01");
        draft.InvoiceNumber.Should().Be("001-001-000000123");
        draft.IssueDate.Should().Be(new DateOnly(2026, 7, 8));
        draft.SriPaymentMethodCode.Should().Be("01");

        draft.Lines.Should().ContainSingle();
        var line = draft.Lines[0];
        line.Description.Should().Be("Producto de prueba");
        line.Quantity.Should().Be(2m);
        line.UnitPrice.Should().Be(10m);
        line.DiscountPct.Should().Be(0m);
        line.VatCode.Should().Be("2");
        line.IceCode.Should().BeNull();

        line.SupplierCode.Should().Be("SKU-001");
        line.SupplierAuxCode.Should().BeNull();
        line.Discount.Should().Be(0m);
        line.LineSubtotal.Should().Be(20m);
        line.TaxCode.Should().Be("2");
        line.VatPercentage.Should().Be(15m);
        line.TaxValue.Should().Be(3m);
        line.TotalLine.Should().Be(23m);
    }

    [Fact]
    public void Parses_a_line_with_ice_and_a_discount_percentage()
    {
        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                Environment: "2", EmissionType: "1", DocTypeCode: "01",
                Establishment: "001", EstablishmentAddress: "Av. 6 de Diciembre",
                EmissionPoint: "002", Sequential: "000000456", IssueDate: new DateTime(2026, 7, 10)),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001", LegalName: "PROVEEDOR ACME S.A.", TradeName: null,
                MatrixAddress: "Av. Amazonas y Naciones Unidas", TaxRegime: "CONTRIBUYENTE RÉGIMEN RIMPE",
                IsAccountingRequired: false),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: "04", IdentificationNumber: "1790012345001",
                LegalName: "Cliente Corporativo S.A.", Address: null, Email: null),
            Details:
            [
                new ElectronicDocumentDetailLine(
                    Code: "SKU-002", Description: "Producto con ICE", Quantity: 1m, UnitPrice: 50m,
                    Discount: 5m, Subtotal: 45m,
                    Taxes:
                    [
                        new ElectronicDocumentDetailTax("VAT", "2", 45m, 15m, 6.75m),
                        new ElectronicDocumentDetailTax("ICE", "3010", 50m, 10m, 5m),
                    ]),
            ],
            TaxSummary:
            [
                new ElectronicDocumentTaxSummary("VAT", "2", 45m, 6.75m),
                new ElectronicDocumentTaxSummary("ICE", "3010", 50m, 5m),
            ],
            Totals: new ElectronicDocumentTotals(45m, 5m, 11.75m, 56.75m, "USD"),
            Payments: [new ElectronicDocumentPayment("20", 56.75m, null, null)],
            AdditionalInfo: []);

        var xml = BuildAuthorizedXml(data);
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.Lines.Should().ContainSingle().Subject;

        line.Quantity.Should().Be(1m);
        line.UnitPrice.Should().Be(50m);
        line.DiscountPct.Should().Be(10m);
        line.VatCode.Should().Be("2");
        line.IceCode.Should().Be("3010");
        line.IceValue.Should().Be(5m);

        line.SupplierCode.Should().Be("SKU-002");
        line.Discount.Should().Be(5m);
        line.LineSubtotal.Should().Be(45m);
        line.VatPercentage.Should().Be(15m);
        line.TaxValue.Should().Be(6.75m);
        line.TotalLine.Should().Be(56.75m);
    }

    [Fact]
    public void Parses_an_exempt_product_with_zero_percent_vat_without_error()
    {
        var data = new ElectronicDocumentData(
            Emission: new ElectronicDocumentEmissionContext(
                Environment: "1", EmissionType: "1", DocTypeCode: "01",
                Establishment: "001", EstablishmentAddress: "Av. Amazonas y Naciones Unidas",
                EmissionPoint: "001", Sequential: "000000789", IssueDate: new DateTime(2026, 7, 8)),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001", LegalName: "PROVEEDOR ACME S.A.", TradeName: "ACME",
                MatrixAddress: "Av. Amazonas y Naciones Unidas", TaxRegime: null, IsAccountingRequired: true),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: "05", IdentificationNumber: "1710034065",
                LegalName: "Empresa Compradora", Address: "Calle Falsa 123", Email: null),
            Details: [new ElectronicDocumentDetailLine(
                Code: "SKU-EXENTO", Description: "Producto exento de IVA", Quantity: 3m, UnitPrice: 5m,
                Discount: 0m, Subtotal: 15m,
                Taxes: [new ElectronicDocumentDetailTax("VAT", "0", 15m, 0m, 0m)])],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "0", 15m, 0m)],
            Totals: new ElectronicDocumentTotals(15m, 0m, 0m, 15m, "USD"),
            Payments: [new ElectronicDocumentPayment("01", 15m, null, null)],
            AdditionalInfo: []);

        var xml = BuildAuthorizedXml(data);
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        // Un producto legítimamente exento trae su propio bloque <impuesto codigo="2"> con
        // tarifa/valor en 0 — el parser genérico ya lo lee sin fabricar ningún valor por defecto.
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.LineErrors.Should().BeEmpty();
        var line = result.Value.Lines.Should().ContainSingle().Subject;
        line.VatCode.Should().Be("0");
        line.VatPercentage.Should().Be(0m);
        line.TaxValue.Should().Be(0m);
    }

    [Fact]
    public void Skips_a_defective_line_without_discarding_the_valid_ones()
    {
        // XML crudo (no vía InvoiceXmlBuilder): el segundo <detalle> no trae <impuestos> — una
        // variación real de un emisor no conforme. La cabecera y el primer detalle son válidos.
        const string xml =
            "<factura><infoTributaria><ruc>1791352688001</ruc><razonSocial>QUALA ECUADOR S A</razonSocial>" +
            "<codDoc>01</codDoc><estab>001</estab><ptoEmi>001</ptoEmi><secuencial>000000123</secuencial>" +
            "</infoTributaria><infoFactura><fechaEmision>01/07/2026</fechaEmision></infoFactura>" +
            "<detalles>" +
            "<detalle><codigoPrincipal>PROV-OK</codigoPrincipal><descripcion>Producto correcto</descripcion>" +
            "<cantidad>1.0000</cantidad><precioUnitario>10.000000</precioUnitario><descuento>0.00</descuento>" +
            "<precioTotalSinImpuesto>10.00</precioTotalSinImpuesto>" +
            "<impuestos><impuesto><codigo>2</codigo><codigoPorcentaje>2</codigoPorcentaje><tarifa>15.00</tarifa>" +
            "<baseImponible>10.00</baseImponible><valor>1.50</valor></impuesto></impuestos></detalle>" +
            "<detalle><codigoPrincipal>PROV-BAD</codigoPrincipal><descripcion>Producto sin impuestos</descripcion>" +
            "<cantidad>1.0000</cantidad><precioUnitario>5.000000</precioUnitario><descuento>0.00</descuento>" +
            "<precioTotalSinImpuesto>5.00</precioTotalSinImpuesto></detalle>" +
            "</detalles></factura>";
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines.Should().ContainSingle();
        result.Value.Lines[0].Description.Should().Be("Producto correcto");
        result.Value.LineErrors.Should().ContainSingle();
        result.Value.LineErrors[0].LineIndex.Should().Be(2);
        result.Value.LineErrors[0].SupplierCode.Should().Be("PROV-BAD");
    }

    [Fact]
    public void Falls_back_to_codigoAuxiliar_when_codigoPrincipal_is_missing()
    {
        const string xml =
            "<factura><infoTributaria><ruc>1791352688001</ruc><razonSocial>QUALA ECUADOR S A</razonSocial>" +
            "<codDoc>01</codDoc><estab>001</estab><ptoEmi>001</ptoEmi><secuencial>000000123</secuencial>" +
            "</infoTributaria><infoFactura><fechaEmision>01/07/2026</fechaEmision></infoFactura>" +
            "<detalles><detalle><codigoAuxiliar>AUX-999</codigoAuxiliar><descripcion>Solo código auxiliar</descripcion>" +
            "<cantidad>1.0000</cantidad><precioUnitario>1.000000</precioUnitario><descuento>0.00</descuento>" +
            "<precioTotalSinImpuesto>1.00</precioTotalSinImpuesto>" +
            "<impuestos><impuesto><codigo>2</codigo><codigoPorcentaje>2</codigoPorcentaje><tarifa>15.00</tarifa>" +
            "<baseImponible>1.00</baseImponible><valor>0.15</valor></impuesto></impuestos></detalle></detalles></factura>";
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines.Should().ContainSingle();
        result.Value.Lines[0].SupplierCode.Should().Be("AUX-999");
    }

    [Fact]
    public void Fails_gracefully_when_the_xml_is_not_a_valid_factura()
    {
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse("<factura><infoTributaria/></factura>");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }
}
