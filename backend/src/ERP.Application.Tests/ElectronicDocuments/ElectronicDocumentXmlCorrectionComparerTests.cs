using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// R12 Fase B — comparación semántica del XML rechazado y el corregido, excluyendo solo la firma.
/// </summary>
public sealed class ElectronicDocumentXmlCorrectionComparerTests
{
    internal const string Key = "3010202601179000000000110010010000000011234567811";

    internal static string Invoice(
        string dirMatriz = "Av. Siempre Viva 123",
        string importeTotal = "11.50",
        string identificacion = "0912345678",
        string valorIva = "1.50",
        string signature = "AAA"
    ) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <factura id="comprobante" version="1.1.0">
          <infoTributaria>
            <ambiente>1</ambiente><tipoEmision>1</tipoEmision><razonSocial>ZH SA</razonSocial>
            <ruc>1790000000001</ruc><claveAcceso>{Key}</claveAcceso><codDoc>01</codDoc>
            <estab>001</estab><ptoEmi>001</ptoEmi><secuencial>000000001</secuencial>
            <dirMatriz>{dirMatriz}</dirMatriz>
          </infoTributaria>
          <infoFactura>
            <fechaEmision>30/10/2026</fechaEmision>
            <tipoIdentificacionComprador>05</tipoIdentificacionComprador>
            <razonSocialComprador>Cliente</razonSocialComprador>
            <identificacionComprador>{identificacion}</identificacionComprador>
            <totalSinImpuestos>10.00</totalSinImpuestos><totalDescuento>0.00</totalDescuento>
            <totalConImpuestos><totalImpuesto><codigo>2</codigo><codigoPorcentaje>4</codigoPorcentaje>
              <baseImponible>10.00</baseImponible><valor>{valorIva}</valor></totalImpuesto></totalConImpuestos>
            <propina>0.00</propina><importeTotal>{importeTotal}</importeTotal><moneda>DOLAR</moneda>
          </infoFactura>
          <detalles><detalle><codigoPrincipal>A1</codigoPrincipal><descripcion>Item</descripcion>
            <cantidad>1.000000</cantidad><precioUnitario>10.000000</precioUnitario>
            <descuento>0.00</descuento><precioTotalSinImpuesto>10.00</precioTotalSinImpuesto></detalle></detalles>
          <ds:Signature xmlns:ds="http://www.w3.org/2000/09/xmldsig#" Id="S1">
            <ds:SignatureValue>{signature}</ds:SignatureValue>
            <ds:Object><etsi:QualifyingProperties xmlns:etsi="http://uri.etsi.org/01903/v1.3.2#">{signature}</etsi:QualifyingProperties></ds:Object>
          </ds:Signature>
        </factura>
        """;

    internal static string Retention(string identificacion = "0990000000001", string dir = "Matriz") =>
        $"""
        <comprobanteRetencion id="comprobante" version="1.0.0">
          <infoTributaria><ambiente>1</ambiente><claveAcceso>{Key}</claveAcceso><dirMatriz>{dir}</dirMatriz></infoTributaria>
          <infoCompRetencion><fechaEmision>30/10/2026</fechaEmision>
            <tipoIdentificacionSujetoRetenido>04</tipoIdentificacionSujetoRetenido>
            <razonSocialSujetoRetenido>Proveedor</razonSocialSujetoRetenido>
            <identificacionSujetoRetenido>{identificacion}</identificacionSujetoRetenido>
            <periodoFiscal>10/2026</periodoFiscal></infoCompRetencion>
          <impuestos><impuesto><codigo>1</codigo><codigoRetencion>312</codigoRetencion>
            <baseImponible>100.00</baseImponible><porcentajeRetener>1.75</porcentajeRetener>
            <valorRetenido>1.75</valorRetenido></impuesto></impuestos>
        </comprobanteRetencion>
        """;

    [Fact]
    public void Resigning_without_changes_is_not_a_correction()
    {
        var result = ElectronicDocumentXmlCorrectionComparer.Compare(
            ElectronicDocumentType.Invoice,
            Invoice(signature: "AAA"),
            Invoice(signature: "BBB").Replace("  ", "    ")
        );

        result.Outcome.Should().Be(ElectronicDocumentXmlCorrectionOutcome.NoCorrection);
    }

    [Fact]
    public void Issuer_or_tax_breakdown_changes_are_demonstrable_corrections()
    {
        var issuer = ElectronicDocumentXmlCorrectionComparer.Compare(
            ElectronicDocumentType.Invoice,
            Invoice(),
            Invoice(dirMatriz: "Av. Corregida 456", signature: "BBB")
        );
        issuer.Outcome.Should().Be(ElectronicDocumentXmlCorrectionOutcome.Corrected);
        issuer.Summary.Should().Contain("dirMatriz").And.NotContain("Signature");

        var tax = ElectronicDocumentXmlCorrectionComparer.Compare(
            ElectronicDocumentType.Invoice,
            Invoice(valorIva: "1.50"),
            Invoice(valorIva: "1.5")
        );
        tax.Outcome.Should().Be(ElectronicDocumentXmlCorrectionOutcome.Corrected);
    }

    [Fact]
    public void Commercial_or_counterparty_changes_are_never_accepted()
    {
        ElectronicDocumentXmlCorrectionComparer
            .Compare(ElectronicDocumentType.Invoice, Invoice(), Invoice(importeTotal: "11.51"))
            .Outcome.Should().Be(ElectronicDocumentXmlCorrectionOutcome.ProtectedDataChanged);

        ElectronicDocumentXmlCorrectionComparer
            .Compare(ElectronicDocumentType.Invoice, Invoice(), Invoice(identificacion: "0999999999"))
            .Outcome.Should().Be(ElectronicDocumentXmlCorrectionOutcome.ProtectedDataChanged);

        ElectronicDocumentXmlCorrectionComparer
            .Compare(
                ElectronicDocumentType.Invoice,
                Invoice(),
                Invoice(dirMatriz: "Otra").Replace("000000001</secuencial>", "000000002</secuencial>")
            )
            .Outcome.Should().Be(ElectronicDocumentXmlCorrectionOutcome.ProtectedDataChanged);
    }

    [Fact]
    public void Retention_subject_identification_change_is_reported_specifically()
    {
        var result = ElectronicDocumentXmlCorrectionComparer.Compare(
            ElectronicDocumentType.Retention,
            Retention(),
            Retention(identificacion: "0990000000002", dir: "Corregida")
        );

        result.Outcome.Should().Be(ElectronicDocumentXmlCorrectionOutcome.SubjectIdentificationChanged);

        ElectronicDocumentXmlCorrectionComparer
            .Compare(ElectronicDocumentType.Retention, Retention(), Retention(dir: "Corregida"))
            .Outcome.Should().Be(ElectronicDocumentXmlCorrectionOutcome.Corrected);
    }

    [Fact]
    public void Unreadable_xml_is_never_a_correction()
    {
        ElectronicDocumentXmlCorrectionComparer
            .Compare(ElectronicDocumentType.Invoice, Invoice(), "<factura>")
            .Outcome.Should().Be(ElectronicDocumentXmlCorrectionOutcome.Unreadable);
    }
}
