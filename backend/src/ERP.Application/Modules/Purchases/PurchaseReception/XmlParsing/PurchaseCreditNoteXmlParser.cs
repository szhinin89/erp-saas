using System.Globalization;
using System.Xml.Linq;
using ERP.Application.Modules.Purchases.PurchaseReception.Services;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;

namespace ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;

public sealed record ParsedPurchaseCreditNoteXml(
    string AccessKey,
    string DocumentNumber,
    DateOnly IssueDate,
    string SupplierRuc,
    string SupplierName,
    string ModifiedDocumentNumber,
    string Reason,
    PurchaseReceptionDetailProcessingResult Detail
);

/// <summary>Snapshot fiscal de notaCredito. No crea compras ni ejecuta Item Matching.</summary>
public static class PurchaseCreditNoteXmlParser
{
    public static ParsedPurchaseCreditNoteXml Parse(string xml, Guid documentId, Guid tenantId)
    {
        var root = XDocument.Parse(xml).Root;
        if (root?.Name != "notaCredito")
            throw new FormatException("El XML recibido no es una nota de crédito.");
        var taxInfo = RequiredElement(root, "infoTributaria");
        if (Text(taxInfo, "codDoc") != "04")
            throw new FormatException("La nota de crédito debe tener codDoc 04.");
        var info = RequiredElement(root, "infoNotaCredito");
        var extras = PurchaseReceptionXmlViewExtractor.Extract(xml);
        var lines = new List<PurchaseReceptionLine>();
        var warnings = new List<string>();
        var details = RequiredElement(root, "detalles").Elements("detalle").ToList();
        foreach (var d in details)
        {
            try
            {
                var taxes = RequiredElement(d, "impuestos").Elements("impuesto")
                    .Select(t => (
                        TaxCode: Text(t, "codigo"),
                        TaxRateCode: Optional(t, "codigoPorcentaje") ?? Text(t, "codigo"),
                        Tarifa: Decimal(t, "tarifa", optional: true),
                        TaxableBase: Decimal(t, "baseImponible"),
                        TaxAmount: Decimal(t, "valor")
                    )).ToList();
                var vat = taxes.FirstOrDefault(t => t.TaxCode == "2");
                var ice = taxes.FirstOrDefault(t => t.TaxCode == "3");
                var quantity = Decimal(d, "cantidad");
                var price = Decimal(d, "precioUnitario");
                var discount = Decimal(d, "descuento");
                var subtotal = Decimal(d, "precioTotalSinImpuesto");
                lines.Add(PurchaseReceptionLine.Create(
                    documentId, tenantId, Text(d, "descripcion"), quantity, price,
                    vat.TaxRateCode, vat.TaxCode, vat.Tarifa, vat.TaxAmount,
                    quantity * price > 0 ? Math.Clamp(Math.Round(discount / (quantity * price) * 100, 2), 0, 100) : 0,
                    discount, subtotal, subtotal + taxes.Sum(t => t.TaxAmount),
                    ice.TaxRateCode, ice.TaxAmount,
                    supplierCode: Optional(d, "codigoInterno"),
                    supplierAuxCode: Optional(d, "codigoAdicional"),
                    taxes: taxes,
                    additionalFields: PurchaseXmlAdditionalFieldReader.Read(d.Element("detallesAdicionales"))
                        .Select(f => (f.Name, f.Value, f.Position))
                ));
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
            {
                warnings.Add($"Línea {lines.Count + warnings.Count + 1}: {ex.Message}");
            }
        }

        var processing = new PurchaseReceptionProcessingOutcome(
            warnings.Count > 0 ? PurchaseReceptionProcessingStatus.ProcessedWithWarnings : PurchaseReceptionProcessingStatus.Processed,
            details.Count, lines.Count, warnings.Count > 0 ? string.Join(" | ", warnings) : null);
        return new ParsedPurchaseCreditNoteXml(
            Text(taxInfo, "claveAcceso"),
            string.Join("-", Text(taxInfo, "estab"), Text(taxInfo, "ptoEmi"), Text(taxInfo, "secuencial")),
            DateOnly.ParseExact(Text(info, "fechaEmision"), "dd/MM/yyyy", CultureInfo.InvariantCulture),
            Text(taxInfo, "ruc"), Text(taxInfo, "razonSocial"),
            Text(info, "numDocModificado"), Text(info, "motivo"),
            new PurchaseReceptionDetailProcessingResult(lines, processing, "04", null, extras.SupplierTradeName));
    }

    private static XElement RequiredElement(XElement parent, string name) =>
        parent.Element(name) ?? throw new FormatException($"Falta el elemento obligatorio '{name}'.");
    private static string? Optional(XElement parent, string name) => parent.Element(name)?.Value.Trim();
    private static string Text(XElement parent, string name) =>
        Optional(parent, name) is { Length: > 0 } value ? value : throw new FormatException($"Falta '{name}'.");
    private static decimal Decimal(XElement parent, string name, bool optional = false) =>
        optional && string.IsNullOrWhiteSpace(Optional(parent, name)) ? 0m :
        decimal.Parse(Text(parent, name), NumberStyles.Number, CultureInfo.InvariantCulture);
}
