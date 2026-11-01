using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;

namespace ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;

/// <summary>Propuesta temporal desde la evidencia existente; nunca selecciona un catálogo.</summary>
public static class PurchaseXmlPaymentTerms
{
    public static PurchaseDraftDto Apply(PurchaseDraftDto draft, string xml)
    {
        try
        {
            var payments = XDocument.Parse(xml).Root?.Element("infoFactura")
                ?.Element("pagos")?.Elements("pago").ToList() ?? [];
            var term = payments.FirstOrDefault()?.Element("plazo")?.Value.Trim();
            var unit = payments.FirstOrDefault()?.Element("unidadTiempo")?.Value.Trim();
            DateOnly? dueDate = null;
            if (payments.Count == 1
                && unit?.ToLowerInvariant() is "dias" or "días" or "dia" or "día"
                && decimal.TryParse(term, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var days)
                && days >= 0 && days == decimal.Truncate(days)
                && days <= DateOnly.MaxValue.DayNumber - draft.IssueDate.DayNumber)
                dueDate = draft.IssueDate.AddDays((int)days);

            return draft with
            {
                XmlPaymentTerm = term,
                XmlPaymentTimeUnit = unit,
                XmlPaymentCount = payments.Count,
                SuggestedDueDate = dueDate,
            };
        }
        catch (XmlException)
        {
            return draft;
        }
    }
}
