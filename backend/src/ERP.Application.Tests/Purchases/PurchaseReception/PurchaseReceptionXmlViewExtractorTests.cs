using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using FluentAssertions;

namespace ERP.Application.Tests.Purchases.PurchaseReception;

/// <summary>
/// <see cref="PurchaseReceptionXmlViewExtractor"/> es puramente de lectura para la vista de XML
/// (FLOW-READY-02E.1) — estas pruebas cubren únicamente lo que la entidad no persiste:
/// nombreComercial, totalConImpuestos, y (nota de crédito) el documento modificado.
/// </summary>
public sealed class PurchaseReceptionXmlViewExtractorTests
{
    private const string CreditNoteXml =
        """
        <notaCredito id="comprobante" version="1.1.0">
          <infoTributaria>
            <ruc>1791352688001</ruc>
            <razonSocial>QUALA ECUADOR S A</razonSocial>
            <nombreComercial>QUALA</nombreComercial>
          </infoTributaria>
          <infoNotaCredito>
            <fechaEmision>05/07/2026</fechaEmision>
            <codDocModificado>01</codDocModificado>
            <numDocModificado>015-027-000161740</numDocModificado>
            <fechaEmisionDocSustento>01/07/2026</fechaEmisionDocSustento>
            <motivo>Devolución de mercadería</motivo>
            <totalSinImpuestos>10.00</totalSinImpuestos>
            <valorModificacion>11.50</valorModificacion>
            <totalConImpuestos>
              <totalImpuesto>
                <codigo>2</codigo>
                <codigoPorcentaje>2</codigoPorcentaje>
                <baseImponible>10.00</baseImponible>
                <valor>1.50</valor>
              </totalImpuesto>
            </totalConImpuestos>
          </infoNotaCredito>
          <detalles>
            <detalle>
              <descripcion>COCA COLA 500 ML</descripcion>
              <cantidad>5</cantidad>
              <precioUnitario>2.00</precioUnitario>
            </detalle>
          </detalles>
        </notaCredito>
        """;

    [Fact]
    public void Extracts_modified_document_and_tax_summary_from_a_credit_note()
    {
        var extras = PurchaseReceptionXmlViewExtractor.Extract(CreditNoteXml);

        extras.SupplierTradeName.Should().Be("QUALA");
        extras.ModifiedDocumentType.Should().Be("01");
        extras.ModifiedDocumentDate.Should().Be(new DateOnly(2026, 7, 1));
        extras.ModificationReason.Should().Be("Devolución de mercadería");
        // El esquema notaCredito no define totalDescuento a nivel de documento.
        extras.DiscountAmount.Should().Be(0m);
        extras.TaxSummaries.Should().ContainSingle();
        extras.TaxSummaries[0].TaxCode.Should().Be("2");
        extras.TaxSummaries[0].TaxRateCode.Should().Be("2");
        extras.TaxSummaries[0].TaxableBase.Should().Be(10.00m);
        extras.TaxSummaries[0].TaxAmount.Should().Be(1.50m);
        extras.Totals.TotalWithoutTaxes.Should().Be(10.00m);
        extras.Totals.TotalAmount.Should().Be(11.50m);
    }

    [Fact]
    public void Returns_empty_extras_for_an_unrecognized_root_element()
    {
        var extras = PurchaseReceptionXmlViewExtractor.Extract("<algoDesconocido/>");

        extras.SupplierTradeName.Should().BeNull();
        extras.TaxSummaries.Should().BeEmpty();
        extras.DiscountAmount.Should().Be(0m);
        extras.IceAmount.Should().Be(0m);
        extras.Lines.Should().BeEmpty();
    }
}
