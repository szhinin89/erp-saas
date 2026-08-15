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
        public string? Resolve(string taxCode) =>
            taxCode switch
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
                Environment: "1",
                EmissionType: "1",
                DocTypeCode: "01",
                Establishment: "001",
                EstablishmentAddress: "Av. Amazonas y Naciones Unidas",
                EmissionPoint: "001",
                Sequential: "000000123",
                IssueDate: new DateTime(2026, 7, 8)
            ),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001",
                LegalName: "PROVEEDOR ACME S.A.",
                TradeName: "ACME",
                MatrixAddress: "Av. Amazonas y Naciones Unidas",
                TaxRegime: null,
                IsAccountingRequired: true
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: "05",
                IdentificationNumber: "1710034065",
                LegalName: "Empresa Compradora",
                Address: "Calle Falsa 123",
                Email: null
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
            Totals: new ElectronicDocumentTotals(20m, 0m, 3m, 23m, "USD"),
            Payments: [new ElectronicDocumentPayment("01", 23m, null, null)],
            AdditionalInfo: []
        );

        var xml = BuildAuthorizedXml(data);
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var draft = result.Value!;

        draft.SupplierRuc.Should().Be("1790012345001");
        draft.SupplierName.Should().Be("PROVEEDOR ACME S.A.");
        draft.SupplierTradeName.Should().Be("ACME");
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
                Environment: "2",
                EmissionType: "1",
                DocTypeCode: "01",
                Establishment: "001",
                EstablishmentAddress: "Av. 6 de Diciembre",
                EmissionPoint: "002",
                Sequential: "000000456",
                IssueDate: new DateTime(2026, 7, 10)
            ),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001",
                LegalName: "PROVEEDOR ACME S.A.",
                TradeName: null,
                MatrixAddress: "Av. Amazonas y Naciones Unidas",
                TaxRegime: "CONTRIBUYENTE RÉGIMEN RIMPE",
                IsAccountingRequired: false
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: "04",
                IdentificationNumber: "1790012345001",
                LegalName: "Cliente Corporativo S.A.",
                Address: null,
                Email: null
            ),
            Details:
            [
                new ElectronicDocumentDetailLine(
                    Code: "SKU-002",
                    Description: "Producto con ICE",
                    Quantity: 1m,
                    UnitPrice: 50m,
                    Discount: 5m,
                    Subtotal: 45m,
                    Taxes:
                    [
                        new ElectronicDocumentDetailTax("VAT", "2", 45m, 15m, 6.75m),
                        new ElectronicDocumentDetailTax("ICE", "3010", 50m, 10m, 5m),
                    ]
                ),
            ],
            TaxSummary:
            [
                new ElectronicDocumentTaxSummary("VAT", "2", 45m, 6.75m),
                new ElectronicDocumentTaxSummary("ICE", "3010", 50m, 5m),
            ],
            Totals: new ElectronicDocumentTotals(45m, 5m, 11.75m, 56.75m, "USD"),
            Payments: [new ElectronicDocumentPayment("20", 56.75m, null, null)],
            AdditionalInfo: []
        );

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
                Environment: "1",
                EmissionType: "1",
                DocTypeCode: "01",
                Establishment: "001",
                EstablishmentAddress: "Av. Amazonas y Naciones Unidas",
                EmissionPoint: "001",
                Sequential: "000000789",
                IssueDate: new DateTime(2026, 7, 8)
            ),
            Issuer: new ElectronicDocumentIssuerData(
                TaxId: "1790012345001",
                LegalName: "PROVEEDOR ACME S.A.",
                TradeName: "ACME",
                MatrixAddress: "Av. Amazonas y Naciones Unidas",
                TaxRegime: null,
                IsAccountingRequired: true
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                IdentificationType: "05",
                IdentificationNumber: "1710034065",
                LegalName: "Empresa Compradora",
                Address: "Calle Falsa 123",
                Email: null
            ),
            Details:
            [
                new ElectronicDocumentDetailLine(
                    Code: "SKU-EXENTO",
                    Description: "Producto exento de IVA",
                    Quantity: 3m,
                    UnitPrice: 5m,
                    Discount: 0m,
                    Subtotal: 15m,
                    Taxes: [new ElectronicDocumentDetailTax("VAT", "0", 15m, 0m, 0m)]
                ),
            ],
            TaxSummary: [new ElectronicDocumentTaxSummary("VAT", "0", 15m, 0m)],
            Totals: new ElectronicDocumentTotals(15m, 0m, 0m, 15m, "USD"),
            Payments: [new ElectronicDocumentPayment("01", 15m, null, null)],
            AdditionalInfo: []
        );

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
            "<factura><infoTributaria><ruc>1791352688001</ruc><razonSocial>QUALA ECUADOR S A</razonSocial>"
            + "<codDoc>01</codDoc><estab>001</estab><ptoEmi>001</ptoEmi><secuencial>000000123</secuencial>"
            + "</infoTributaria><infoFactura><fechaEmision>01/07/2026</fechaEmision></infoFactura>"
            + "<detalles>"
            + "<detalle><codigoPrincipal>PROV-OK</codigoPrincipal><descripcion>Producto correcto</descripcion>"
            + "<cantidad>1.0000</cantidad><precioUnitario>10.000000</precioUnitario><descuento>0.00</descuento>"
            + "<precioTotalSinImpuesto>10.00</precioTotalSinImpuesto>"
            + "<impuestos><impuesto><codigo>2</codigo><codigoPorcentaje>2</codigoPorcentaje><tarifa>15.00</tarifa>"
            + "<baseImponible>10.00</baseImponible><valor>1.50</valor></impuesto></impuestos></detalle>"
            + "<detalle><codigoPrincipal>PROV-BAD</codigoPrincipal><descripcion>Producto sin impuestos</descripcion>"
            + "<cantidad>1.0000</cantidad><precioUnitario>5.000000</precioUnitario><descuento>0.00</descuento>"
            + "<precioTotalSinImpuesto>5.00</precioTotalSinImpuesto></detalle>"
            + "</detalles></factura>";
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
            "<factura><infoTributaria><ruc>1791352688001</ruc><razonSocial>QUALA ECUADOR S A</razonSocial>"
            + "<codDoc>01</codDoc><estab>001</estab><ptoEmi>001</ptoEmi><secuencial>000000123</secuencial>"
            + "</infoTributaria><infoFactura><fechaEmision>01/07/2026</fechaEmision></infoFactura>"
            + "<detalles><detalle><codigoAuxiliar>AUX-999</codigoAuxiliar><descripcion>Solo código auxiliar</descripcion>"
            + "<cantidad>1.0000</cantidad><precioUnitario>1.000000</precioUnitario><descuento>0.00</descuento>"
            + "<precioTotalSinImpuesto>1.00</precioTotalSinImpuesto>"
            + "<impuestos><impuesto><codigo>2</codigo><codigoPorcentaje>2</codigoPorcentaje><tarifa>15.00</tarifa>"
            + "<baseImponible>1.00</baseImponible><valor>0.15</valor></impuesto></impuestos></detalle></detalles></factura>";
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Lines.Should().ContainSingle();
        result.Value.Lines[0].SupplierCode.Should().Be("AUX-999");
    }

    [Fact]
    public void Captures_ivbpnr_codigo_5_generically_without_discarding_it()
    {
        // Caso real: factura Arca Continental, línea FANTA HARMONY NRJ 1350 PET(12) con
        // IVA(2)/ICE(3053)/IRBPNR(5). El parser generaba ParsedPurchaseXmlLine sin capturar el
        // nodo <impuesto codigo="5"> — este test fija que ahora sí queda en Taxes, sin tocar el
        // comportamiento existente de VatCode/IceCode/IceValue.
        const string xml =
            "<factura><infoTributaria><ruc>1790000000001</ruc><razonSocial>ARCA CONTINENTAL ECUADOR</razonSocial>"
            + "<codDoc>01</codDoc><estab>001</estab><ptoEmi>001</ptoEmi><secuencial>000000999</secuencial>"
            + "</infoTributaria><infoFactura><fechaEmision>05/08/2026</fechaEmision></infoFactura>"
            + "<detalles><detalle><codigoPrincipal>7861234560001</codigoPrincipal>"
            + "<descripcion>FANTA HARMONY NRJ 1350 PET(12)</descripcion>"
            + "<cantidad>2.0000</cantidad><precioUnitario>7.0044</precioUnitario><descuento>0.00</descuento>"
            + "<precioTotalSinImpuesto>14.01</precioTotalSinImpuesto>"
            + "<impuestos>"
            + "<impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>4.00</tarifa>"
            + "<baseImponible>14.01</baseImponible><valor>0.56</valor></impuesto>"
            + "<impuesto><codigo>3</codigo><codigoPorcentaje>3053</codigoPorcentaje><tarifa>0.00</tarifa>"
            + "<baseImponible>14.01</baseImponible><valor>1.23</valor></impuesto>"
            + "<impuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><tarifa>0.02</tarifa>"
            + "<baseImponible>24</baseImponible><valor>0.48</valor></impuesto>"
            + "</impuestos></detalle></detalles></factura>";
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.Lines.Should().ContainSingle().Subject;

        // Comportamiento existente (VatCode/IceCode/IceValue) sin cambios — regresión.
        line.VatCode.Should().Be("4");
        line.IceCode.Should().Be("3053");
        line.IceValue.Should().Be(1.23m);

        // Nuevo: los 3 impuestos quedan capturados genéricamente, incluyendo IRBPNR (código "5"),
        // que antes se descartaba por completo.
        line.Taxes.Should().HaveCount(3);
        line.Taxes.Should().Contain(t => t.TaxCode == "2" && t.TaxRateCode == "4" && t.TaxAmount == 0.56m);
        line.Taxes.Should().Contain(t => t.TaxCode == "3" && t.TaxRateCode == "3053" && t.TaxAmount == 1.23m);
        var irbpnr = line.Taxes.Should().ContainSingle(t => t.TaxCode == "5").Subject;
        irbpnr.TaxRateCode.Should().Be("5001");
        irbpnr.TaxAmount.Should().Be(0.48m);
        irbpnr.TaxableBase.Should().Be(24m);
        irbpnr.Tarifa.Should().Be(0.02m);
    }

    [Fact]
    public void Parses_the_real_ArcaContinental_XML_reported_by_the_user_with_IRBPNR_on_both_lines()
    {
        // Caso real de producción (no reconstruido con InvoiceXmlBuilder): factura 029-001-001293714,
        // BEBIDAS ARCACONTINENTAL, reportada por el usuario porque la línea INCA-KOLA (codigoPrincipal
        // 12469) no mostraba IRBPNR en "compra nueva". El XML se pega TAL CUAL vino (cabecera
        // recortada a los nodos que PurchaseXmlDraftParser exige con RequireText/RequireElement).
        //
        // IMPORTANTE: el usuario originalmente atribuyó IVA=1.61/IRBPNR=0.24/Total=14.83 a INCA-KOLA,
        // pero esos valores son en realidad de la PRIMERA línea del XML (SPRITE HARMONY 1350 PET(12)).
        // INCA-KOLA (segunda línea) trae IVA=1.95/IRBPNR=0.72/Total=15.65. Este test verifica ambas
        // líneas con sus valores reales correctos, sin mezclarlos.
        const string xml =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <factura id="comprobante" version="2.1.0">
              <infoTributaria>
                <ambiente>2</ambiente>
                <tipoEmision>1</tipoEmision>
                <razonSocial>BEBIDAS ARCACONTINENTAL ECUADOR ARCADOR C.L.</razonSocial>
                <nombreComercial>BEBIDAS ARCACONTINENTAL ECUADOR ARCADOR C.L.</nombreComercial>
                <ruc>1792411149001</ruc>
                <claveAcceso>0108202601179241114900120290010012937140129371419</claveAcceso>
                <codDoc>01</codDoc>
                <estab>029</estab>
                <ptoEmi>001</ptoEmi>
                <secuencial>001293714</secuencial>
                <dirMatriz>PANAMERICANA NORTE OE9-166 Y JOSE VITERI KM 15</dirMatriz>
              </infoTributaria>
              <infoFactura>
                <fechaEmision>01/08/2026</fechaEmision>
                <dirEstablecimiento>AV. 16 DE ABRIL , Y CALLE S/N</dirEstablecimiento>
                <contribuyenteEspecial>00082</contribuyenteEspecial>
                <obligadoContabilidad>SI</obligadoContabilidad>
                <tipoIdentificacionComprador>05</tipoIdentificacionComprador>
                <razonSocialComprador>ZHININ ZHININ SEGUNDO FERNANDO - ZHININ ZHININ SEGUNDO FERNANDO</razonSocialComprador>
                <identificacionComprador>0350016432</identificacionComprador>
                <direccionComprador>CA AR                         CENTRO</direccionComprador>
                <totalSinImpuestos>74.39</totalSinImpuestos>
                <totalDescuento>0.00</totalDescuento>
                <totalConImpuestos>
                  <totalImpuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><baseImponible>80.84</baseImponible><tarifa>15.00</tarifa><valor>12.13</valor></totalImpuesto>
                  <totalImpuesto><codigo>3</codigo><codigoPorcentaje>3053</codigoPorcentaje><baseImponible>35.81</baseImponible><tarifa>0.18</tarifa><valor>6.45</valor></totalImpuesto>
                  <totalImpuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><baseImponible>132.00</baseImponible><tarifa>0.02</tarifa><valor>2.64</valor></totalImpuesto>
                </totalConImpuestos>
                <propina>0.00</propina>
                <importeTotal>95.60</importeTotal>
                <moneda>DOLAR</moneda>
                <pagos><pago><formaPago>01</formaPago><total>95.60</total><plazo>8</plazo><unidadTiempo>dias</unidadTiempo></pago></pagos>
                <valorRetIva>0</valorRetIva>
                <valorRetRenta>0</valorRetRenta>
              </infoFactura>
              <detalles>
                <detalle>
                  <codigoPrincipal>0580</codigoPrincipal>
                  <descripcion>SPRITE HARMONY 1350 PET(12)</descripcion>
                  <cantidad>1.000000</cantidad>
                  <precioUnitario>10.72130</precioUnitario>
                  <descuento>0.00</descuento>
                  <precioTotalSinImpuesto>10.72</precioTotalSinImpuesto>
                  <impuestos>
                    <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>10.72</baseImponible><valor>1.61</valor></impuesto>
                    <impuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><tarifa>0.02</tarifa><baseImponible>12.00</baseImponible><valor>0.24</valor></impuesto>
                  </impuestos>
                </detalle>
                <detalle>
                  <codigoPrincipal>12469</codigoPrincipal>
                  <descripcion>INCA-KOLA ORGL 900ML PET NR 12</descripcion>
                  <cantidad>3.000000</cantidad>
                  <precioUnitario>4.32817</precioUnitario>
                  <descuento>0.00</descuento>
                  <precioTotalSinImpuesto>12.98</precioTotalSinImpuesto>
                  <detallesAdicionales>
                    <detAdicional nombre="Unidad" valor="3 /  0"/>
                    <detAdicional nombre="valor2" valor="0.72"/>
                  </detallesAdicionales>
                  <impuestos>
                    <impuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje><tarifa>15.00</tarifa><baseImponible>12.98</baseImponible><valor>1.95</valor></impuesto>
                    <impuesto><codigo>5</codigo><codigoPorcentaje>5001</codigoPorcentaje><tarifa>0.02</tarifa><baseImponible>36.00</baseImponible><valor>0.72</valor></impuesto>
                  </impuestos>
                </detalle>
              </detalles>
            </factura>
            """;
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var draft = result.Value!;
        draft.LineErrors.Should().BeEmpty();
        draft.Lines.Should().HaveCount(2);

        // Línea 1: SPRITE HARMONY 1350 PET(12) — codigoPrincipal 0580.
        var sprite = draft.Lines[0];
        sprite.Description.Should().Be("SPRITE HARMONY 1350 PET(12)");
        sprite.SupplierCode.Should().Be("0580");
        sprite.Quantity.Should().Be(1m);
        sprite.UnitPrice.Should().Be(10.72130m);
        sprite.LineSubtotal.Should().Be(10.72m);
        sprite.TaxValue.Should().Be(1.61m);
        sprite.TotalLine.Should().Be(10.72m + 1.61m + 0.24m);
        sprite.Taxes.Should().HaveCount(2);
        sprite.Taxes.Should().Contain(t => t.TaxCode == "2" && t.TaxAmount == 1.61m);
        var spriteIrbpnr = sprite.Taxes.Should().ContainSingle(t => t.TaxCode == "5").Subject;
        spriteIrbpnr.TaxAmount.Should().Be(0.24m);
        // SPRITE no trae detallesAdicionales en el XML real — lista vacía, nunca null.
        sprite.AdditionalFields.Should().BeEmpty();

        // Línea 2: INCA-KOLA ORGL 900ML PET NR 12 — codigoPrincipal 12469, el caso reportado.
        var incaKola = draft.Lines[1];
        incaKola.Description.Should().Be("INCA-KOLA ORGL 900ML PET NR 12");
        incaKola.SupplierCode.Should().Be("12469");
        incaKola.Quantity.Should().Be(3m);
        incaKola.UnitPrice.Should().Be(4.32817m);
        incaKola.LineSubtotal.Should().Be(12.98m);
        incaKola.TaxValue.Should().Be(1.95m);
        incaKola.TotalLine.Should().Be(12.98m + 1.95m + 0.72m);
        incaKola.Taxes.Should().HaveCount(2);
        incaKola.Taxes.Should().Contain(t => t.TaxCode == "2" && t.TaxAmount == 1.95m);
        var incaKolaIrbpnr = incaKola.Taxes.Should().ContainSingle(t => t.TaxCode == "5").Subject;
        incaKolaIrbpnr.TaxRateCode.Should().Be("5001");
        incaKolaIrbpnr.TaxAmount.Should().Be(0.72m);
        incaKolaIrbpnr.TaxableBase.Should().Be(36.00m);
        incaKolaIrbpnr.Tarifa.Should().Be(0.02m);

        // PURCHASE-XML-LINE-ADDITIONAL-FIELDS-01 — caso real reportado: la línea INCA-KOLA trae
        // detallesAdicionales que antes se descartaban por completo. "3 /  0" preserva el doble
        // espacio tal como lo declaró el proveedor — no se recorta ni normaliza.
        incaKola.AdditionalFields.Should().HaveCount(2);
        incaKola.AdditionalFields.Should().ContainSingle(f => f.Name == "Unidad" && f.Value == "3 /  0" && f.Position == 0);
        incaKola.AdditionalFields.Should().ContainSingle(f => f.Name == "valor2" && f.Value == "0.72" && f.Position == 1);
    }

    [Fact]
    public void Returns_an_empty_additional_fields_list_when_the_line_has_no_detallesAdicionales()
    {
        const string xml =
            "<factura><infoTributaria><ruc>1791352688001</ruc><razonSocial>QUALA ECUADOR S A</razonSocial>"
            + "<codDoc>01</codDoc><estab>001</estab><ptoEmi>001</ptoEmi><secuencial>000000123</secuencial>"
            + "</infoTributaria><infoFactura><fechaEmision>01/07/2026</fechaEmision></infoFactura>"
            + "<detalles><detalle><codigoPrincipal>PROV-OK</codigoPrincipal><descripcion>Producto sin datos adicionales</descripcion>"
            + "<cantidad>1.0000</cantidad><precioUnitario>10.000000</precioUnitario><descuento>0.00</descuento>"
            + "<precioTotalSinImpuesto>10.00</precioTotalSinImpuesto>"
            + "<impuestos><impuesto><codigo>2</codigo><codigoPorcentaje>2</codigoPorcentaje><tarifa>15.00</tarifa>"
            + "<baseImponible>10.00</baseImponible><valor>1.50</valor></impuesto></impuestos></detalle></detalles></factura>";
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.Lines.Should().ContainSingle().Subject;
        line.AdditionalFields.Should().NotBeNull();
        line.AdditionalFields.Should().BeEmpty();
    }

    [Fact]
    public void Preserves_repeated_detAdicional_names_without_overwriting_either_value()
    {
        // El esquema SRI permite hasta 3 <detAdicional> por línea, sin exigir nombres únicos — un
        // emisor puede declarar el mismo "LOTE" dos veces (p. ej. mercadería de dos lotes en una
        // sola línea de factura). Ambos valores deben preservarse, nunca sobrescribirse.
        const string xml =
            "<factura><infoTributaria><ruc>1791352688001</ruc><razonSocial>QUALA ECUADOR S A</razonSocial>"
            + "<codDoc>01</codDoc><estab>001</estab><ptoEmi>001</ptoEmi><secuencial>000000123</secuencial>"
            + "</infoTributaria><infoFactura><fechaEmision>01/07/2026</fechaEmision></infoFactura>"
            + "<detalles><detalle><codigoPrincipal>PROV-LOTE</codigoPrincipal><descripcion>Producto con dos lotes</descripcion>"
            + "<cantidad>1.0000</cantidad><precioUnitario>10.000000</precioUnitario><descuento>0.00</descuento>"
            + "<precioTotalSinImpuesto>10.00</precioTotalSinImpuesto>"
            + "<detallesAdicionales>"
            + "<detAdicional nombre=\"LOTE\" valor=\"A4821\"/>"
            + "<detAdicional nombre=\"LOTE\" valor=\"B1190\"/>"
            + "</detallesAdicionales>"
            + "<impuestos><impuesto><codigo>2</codigo><codigoPorcentaje>2</codigoPorcentaje><tarifa>15.00</tarifa>"
            + "<baseImponible>10.00</baseImponible><valor>1.50</valor></impuesto></impuestos></detalle></detalles></factura>";
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.Lines.Should().ContainSingle().Subject;
        line.AdditionalFields.Should().HaveCount(2);
        line.AdditionalFields.Should().Contain(f => f.Name == "LOTE" && f.Value == "A4821" && f.Position == 0);
        line.AdditionalFields.Should().Contain(f => f.Name == "LOTE" && f.Value == "B1190" && f.Position == 1);
    }

    [Fact]
    public void Preserves_additional_field_names_and_values_with_accents_and_special_characters_as_is()
    {
        const string xml =
            "<factura><infoTributaria><ruc>1791352688001</ruc><razonSocial>QUALA ECUADOR S A</razonSocial>"
            + "<codDoc>01</codDoc><estab>001</estab><ptoEmi>001</ptoEmi><secuencial>000000123</secuencial>"
            + "</infoTributaria><infoFactura><fechaEmision>01/07/2026</fechaEmision></infoFactura>"
            + "<detalles><detalle><codigoPrincipal>PROV-CAD</codigoPrincipal><descripcion>Producto perecible</descripcion>"
            + "<cantidad>1.0000</cantidad><precioUnitario>10.000000</precioUnitario><descuento>0.00</descuento>"
            + "<precioTotalSinImpuesto>10.00</precioTotalSinImpuesto>"
            + "<detallesAdicionales>"
            + "<detAdicional nombre=\"FECHA VENCIMIENTO\" valor=\"31/12/2027\"/>"
            + "<detAdicional nombre=\"Número de Serie\" valor=\"S/N° X-001 (línea A)\"/>"
            + "</detallesAdicionales>"
            + "<impuestos><impuesto><codigo>2</codigo><codigoPorcentaje>2</codigoPorcentaje><tarifa>15.00</tarifa>"
            + "<baseImponible>10.00</baseImponible><valor>1.50</valor></impuesto></impuestos></detalle></detalles></factura>";
        var parser = new PurchaseXmlDraftParser();

        var result = parser.Parse(xml);

        result.IsSuccess.Should().BeTrue(result.Error);
        var line = result.Value!.Lines.Should().ContainSingle().Subject;
        line.AdditionalFields.Should().HaveCount(2);
        line.AdditionalFields.Should().Contain(f => f.Name == "FECHA VENCIMIENTO" && f.Value == "31/12/2027");
        line.AdditionalFields.Should().Contain(f =>
            f.Name == "Número de Serie" && f.Value == "S/N° X-001 (línea A)"
        );
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
