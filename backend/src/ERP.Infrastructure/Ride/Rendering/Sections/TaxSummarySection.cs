using ERP.Application.Modules.Ride.Templates;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace ERP.Infrastructure.Ride.Rendering.Sections;

/// <summary>
/// Caja de Totales — formato clonado del PDF de referencia (Fase 14): a diferencia de la fase
/// anterior, la referencia NO tiene una caja "Impuestos" (Base Imponible/Valor) separada — el
/// desglose por tarifa ya vive en las filas "SUBTOTAL {tarifa}%"/"IVA {tarifa}%" de esta misma
/// caja, por lo que esa tabla adicional se eliminó (redundante, nunca visible en la referencia).
/// <see cref="ERP.Infrastructure.Ride.Rendering.QuestPdfRideRenderer"/> ubica esta caja junto a
/// Información Adicional + Formas de Pago, en la misma fila, tal como la referencia.
///
/// Los subtotales "Subtotal {tarifa}%" se agregan sumando <c>TaxableBase</c> de
/// <c>Lines[].Taxes[]</c> agrupado por tarifa — es la única fuente donde
/// <see cref="ERP.Domain.Modules.Ride.ValueObjects.RideTaxSummary.Rate"/> viene poblado (el
/// resumen a nivel documento, <c>TaxSummary</c>, no trae tarifa — ver
/// <c>InvoiceRideXmlParser.ParseDocumentTax</c>). Es una suma de bases ya presentes en el XML
/// autorizado, nunca un recálculo de impuestos: los valores de IVA/ICE que sí se muestran
/// (filas "IVA"/"ICE") provienen tal cual de <c>TaxSummary</c>, sin tocarlos.
///
/// <c>TaxCode</c> "2"/"3"/"5" son los códigos fijos del esquema XSD del SRI (IVA/ICE/IRBPNR,
/// Ficha Técnica Anexo 1) — una constante estructural del formato, no un catálogo de tarifas
/// (mismo criterio ya usado en este renderer para Ambiente/Emisión).
/// </summary>
public static class TaxSummarySection
{
    private const string VatTaxCode = "2";
    private const string IceTaxCode = "3";
    private const string IrbpnrTaxCode = "5";

    // Códigos fijos de codigoPorcentaje del esquema SRI (Ficha Técnica) para las dos categorías
    // especiales de IVA — a diferencia de los códigos de tarifa numérica (que sí cambian con el
    // tiempo, p. ej. 12%→15%), "No objeto"/"Exento" son categorías estructurales que nunca han
    // cambiado de significado. Se leen del TaxSummary a nivel documento (que sí trae
    // codigoPorcentaje, a diferencia de Rate) — el valor mostrado es siempre la suma real de lo
    // que haya en el XML para esa categoría, nunca 0 fijo: si no hay ventas exentas, la suma real
    // da 0; si las hay, se refleja el monto real.
    private const string NotSubjectToVatPercentageCode = "6";
    private const string ExemptFromVatPercentageCode = "7";

    // Tarifas de IVA vigentes en Ecuador a la fecha de este cierre (15% general, 5% reducida,
    // 0% tarifa cero) — se muestran siempre, con 0.00 si no aplican, por instrucción explícita
    // del usuario (formato oficial nunca oculta filas). Cualquier otra tarifa real presente en
    // el XML (p. ej. una transitoria) se agrega dinámicamente sin reemplazarlas.
    private static readonly decimal[] StandardVatRates = [15m, 5m, 0m];

    public static void Compose(IContainer container, InvoiceRideDocumentLayout layout)
    {
        var header = layout.Header;

        // Única fuente donde Rate viene poblado (RideTaxSummary a nivel documento no lo trae —
        // ver InvoiceRideXmlParser.ParseDocumentTax). "No objeto"/"Exento" se excluyen de esta
        // agrupación por tarifa numérica — tienen sus propias filas dedicadas más abajo (por
        // codigoPorcentaje, no por Rate) para no contar dos veces la misma base imponible.
        var actualRatesFound = layout
            .Lines.SelectMany(l => l.Taxes)
            .Where(t =>
                t.TaxCode == VatTaxCode
                && t.Rate.HasValue
                && t.TaxPercentageCode != NotSubjectToVatPercentageCode
                && t.TaxPercentageCode != ExemptFromVatPercentageCode
            )
            .GroupBy(t => t.Rate!.Value)
            .ToDictionary(
                g => g.Key,
                g => (Base: g.Sum(t => t.TaxableBase), Amount: g.Sum(t => t.TaxAmount))
            );

        // Las tarifas estándar siempre se muestran (0.00 si no aplican); cualquier tarifa real
        // adicional encontrada en el XML se agrega sin reemplazarlas — nunca se oculta un valor
        // real por no estar en la lista estándar.
        var vatByRate = StandardVatRates
            .Concat(actualRatesFound.Keys)
            .Distinct()
            .OrderByDescending(rate => rate)
            .Select(rate =>
            {
                var (baseAmount, amount) = actualRatesFound.TryGetValue(rate, out var found)
                    ? found
                    : (0m, 0m);
                return (Rate: rate, Base: baseAmount, Amount: amount);
            })
            .ToList();

        var notSubjectToVatBase = layout
            .TaxSummary.Where(t =>
                t.TaxCode == VatTaxCode && t.TaxPercentageCode == NotSubjectToVatPercentageCode
            )
            .Sum(t => t.TaxableBase);
        var exemptFromVatBase = layout
            .TaxSummary.Where(t =>
                t.TaxCode == VatTaxCode && t.TaxPercentageCode == ExemptFromVatPercentageCode
            )
            .Sum(t => t.TaxableBase);
        var iceTotal = layout.TaxSummary.Where(t => t.TaxCode == IceTaxCode).Sum(t => t.TaxAmount);
        var irbpnrTotal = layout
            .TaxSummary.Where(t => t.TaxCode == IrbpnrTaxCode)
            .Sum(t => t.TaxAmount);

        container
            .RideBox()
            .Padding(8)
            .Column(column =>
            {
                column.Spacing(2);

                foreach (var (rate, baseAmount, _) in vatByRate)
                    TotalRow(
                        column,
                        $"SUBTOTAL {rate.ToString("0.##", CultureInfo.InvariantCulture)}%",
                        baseAmount,
                        bold: false
                    );

                TotalRow(column, "SUBTOTAL NO OBJETO DE IVA", notSubjectToVatBase, bold: false);
                TotalRow(column, "SUBTOTAL EXENTO DE IVA", exemptFromVatBase, bold: false);
                TotalRow(column, "SUBTOTAL SIN IMPUESTOS", header.SubtotalWithoutTax, bold: false);
                TotalRow(column, "TOTAL DESCUENTO", header.TotalDiscount, bold: false);
                TotalRow(column, "ICE", iceTotal, bold: false);

                foreach (var (rate, _, amount) in vatByRate)
                    TotalRow(
                        column,
                        $"IVA {rate.ToString("0.##", CultureInfo.InvariantCulture)}%",
                        amount,
                        bold: false
                    );

                TotalRow(column, "IRBPNR", irbpnrTotal, bold: false);
                TotalRow(column, "PROPINA", header.Tip, bold: false);
                TotalRow(column, "VALOR TOTAL", header.GrandTotal, bold: true);

                // Siempre 0.00: RideModel no modela subsidios (combustibles) — ningún dato real
                // existe para calcular esto distinto de cero, mismo criterio que IRBPNR/ICE cuando
                // no aplican. No es un valor inventado: es la ausencia real de esa característica.
                TotalRow(column, "VALOR TOTAL SIN SUBSIDIO", 0m, bold: false);
                TotalRow(column, "AHORRO POR SUBSIDIO", 0m, bold: false);
            });
    }

    private static void TotalRow(ColumnDescriptor column, string label, decimal amount, bool bold)
    {
        column
            .Item()
            .Row(r =>
            {
                if (bold)
                {
                    r.RelativeItem().Text(label).Bold().FontSize(9);
                    r.ConstantItem(70)
                        .AlignRight()
                        .Text(amount.ToString("F2", CultureInfo.InvariantCulture))
                        .Bold()
                        .FontSize(9);
                }
                else
                {
                    r.RelativeItem().Text(label).FontSize(8);
                    r.ConstantItem(70)
                        .AlignRight()
                        .Text(amount.ToString("F2", CultureInfo.InvariantCulture))
                        .FontSize(8);
                }
            });
    }
}
