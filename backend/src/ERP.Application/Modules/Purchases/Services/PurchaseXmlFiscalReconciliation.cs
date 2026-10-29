using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Application.Modules.Purchases.Services;

internal static class PurchaseXmlFiscalReconciliation
{
    private const string Invalid = "Los costos operativos o impuestos no concilian con el XML por categoria y tarifa fiscal.";
    private static decimal Money(decimal value) => Math.Round(value, FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);
    // Maximum uncertainty from rounding each contributing amount and the aggregate independently.
    private static bool RoundsTogether(decimal sum, decimal total, int count)
        => Math.Abs(sum - total) < (count + 1) / (2m * (decimal)Math.Pow(10, FiscalPrecision.TaxAmount));

    public static string? Reconcile(PurchaseInvoice invoice, PurchaseReceptionDocument source, ParsedPurchaseXml xml)
    {
        var header = PurchaseReceptionXmlViewExtractor.Extract(source.XmlContent!);
        if (header.Totals.TotalAmount is not { } total || header.TaxSummaries.Count == 0
            || header.TaxSummaries.Any(t => t.TaxCode != SriTaxCategoryCodes.Vat && t.TaxCode != SriTaxCategoryCodes.Ice && t.TaxCode != SriTaxCategoryCodes.Irbpnr)
            || header.TaxSummaries.GroupBy(t => (t.TaxCode, t.TaxRateCode)).Any(g => g.Count() != 1)
            || header.Totals.Tip.GetValueOrDefault() != 0
            || Money(xml.TotalWithoutTaxes!.Value + header.TaxSummaries.Sum(t => t.TaxAmount)) != total)
            return Invalid;

        var originals = source.Lines.ToDictionary(l => l.Id);
        var rows = new Dictionary<Guid, List<PurchaseInvoiceDetailTax>>();
        foreach (var line in invoice.Lines)
        {
            var original = originals[line.PurchaseReceptionLineId!.Value];
            if (original.Taxes.Count == 0 || line.VatCode != original.VatCode
                || line.IceCode != original.IceCode
                || line.Taxes.Count != original.Taxes.Count
                || line.Taxes.GroupBy(t => t.TaxCode).Any(g => g.Count() != 1)
                || line.Taxes.Any(t => !original.Taxes.Any(o => o.TaxCode == t.TaxCode
                    && o.TaxRateCode == t.TaxRateCode && o.Tarifa == t.Rate)))
                return Invalid;
            rows[line.Id] = line.Taxes.ToList();
        }

        var fiscalGroups = source.Lines.SelectMany(l => l.Taxes).GroupBy(t => (t.TaxCode, t.TaxRateCode)).ToList();
        if (fiscalGroups.Count != header.TaxSummaries.Count) return Invalid;
        foreach (var group in fiscalGroups)
        {
            var declared = header.TaxSummaries.SingleOrDefault(t => (t.TaxCode, t.TaxRateCode) == group.Key);
            if (declared is null || declared.TaxAmount < 0 || declared.TaxableBase < 0
                || group.Select(t => t.Tarifa).Distinct().Count() != 1
                || !RoundsTogether(group.Sum(t => t.TaxableBase), declared.TaxableBase, group.Count())) return Invalid;
            var rate = group.First().Tarifa;
            if (declared.TaxRate.HasValue && declared.TaxRate != rate) return Invalid;
            var members = invoice.Lines.Where(l => rows[l.Id].Any(t => (t.TaxCode, t.TaxRateCode) == group.Key)).ToList();
            var sample = rows[members[0].Id].Single(t => (t.TaxCode, t.TaxRateCode) == group.Key);
            if (sample.CalculationType == SriTaxCalculationType.Percentage
                && group.Key.TaxCode != SriTaxCategoryCodes.Irbpnr)
            {
                if (Money(declared.TaxableBase * rate / 100m) != declared.TaxAmount
                    || !RoundsTogether(group.Sum(t => t.TaxAmount), declared.TaxAmount, group.Count())) return Invalid;
            }
            else if (group.Sum(t => t.TaxAmount) != declared.TaxAmount) return Invalid;

            var baseDelta = declared.TaxableBase - members.Sum(l => rows[l.Id].Single(t => (t.TaxCode, t.TaxRateCode) == group.Key).TaxableBase);
            var taxDelta = declared.TaxAmount - members.Sum(l => rows[l.Id].Single(t => (t.TaxCode, t.TaxRateCode) == group.Key).TaxAmount);
            var target = members.Where(l => !l.IsXmlBreakdown && l.OperationalCostBase > 0 && !invoice.CostDistributions.Any(d => d.SourceInvoiceDetailId == l.Id))
                .OrderByDescending(l => l.OperationalCostBase).ThenBy(l => l.SortOrder).FirstOrDefault();
            if ((baseDelta != 0 || taxDelta != 0) && target is null) return Invalid;
            foreach (var line in members.Where(l => !l.IsXmlBreakdown))
            {
                var old = rows[line.Id].Single(t => (t.TaxCode, t.TaxRateCode) == group.Key);
                var basis = old.TaxableBase + (line == target ? baseDelta : 0);
                var amount = old.TaxAmount + (line == target ? taxDelta : 0);
                if (basis < 0 || amount < 0) return Invalid;
                rows[line.Id].Remove(old);
                rows[line.Id].Add(PurchaseInvoiceDetailTax.Create(line.Id, line.TenantId, old.TaxCode,
                    old.TaxRateCode, old.TaxName, old.Rate, old.CalculationType, basis, amount, PurchaseTaxSource.Xml));
            }
        }

        // Reconcile operational costs within each fiscal classification, never between rates/categories.
        foreach (var group in invoice.Lines.GroupBy(l => string.Join(";", rows[l.Id]
            .OrderBy(t => t.TaxCode).Select(t => $"{t.TaxCode}:{t.TaxRateCode}"))))
        {
            var members = group.ToList();
            var cost = members.Sum(l => l.OperationalCostBase);
            var originalBase = members.Select(l => originals[l.PurchaseReceptionLineId!.Value]).Distinct().Sum(l => l.LineSubtotal);
            if (!RoundsTogether(cost, originalBase, members.Count)) return Invalid;
            var vatBase = members.Sum(l => rows[l.Id].Single(t => t.TaxCode == SriTaxCategoryCodes.Vat).TaxableBase);
            var ice = members.Sum(l => rows[l.Id].Where(t => t.TaxCode == SriTaxCategoryCodes.Ice).Sum(t => t.TaxAmount));
            var delta = vatBase - ice - cost;
            if (!RoundsTogether(cost, vatBase - ice, members.Count)) return Invalid;
            var target = members.Where(l => !l.IsXmlBreakdown && l.OperationalCostBase > 0 && !invoice.CostDistributions.Any(d => d.SourceInvoiceDetailId == l.Id))
                .OrderByDescending(l => l.OperationalCostBase).ThenBy(l => l.SortOrder).FirstOrDefault();
            if (delta != 0 && target is null) return Invalid;
            foreach (var line in members.Where(l => !l.IsXmlBreakdown))
            {
                var old = rows[line.Id].Single(t => t.TaxCode == SriTaxCategoryCodes.Vat);
                var basis = line.OperationalCostBase + rows[line.Id].Where(t => t.TaxCode == SriTaxCategoryCodes.Ice).Sum(t => t.TaxAmount)
                    + (line == target ? delta : 0);
                if (basis < 0) return Invalid;
                rows[line.Id].Remove(old);
                rows[line.Id].Add(PurchaseInvoiceDetailTax.Create(line.Id, line.TenantId, old.TaxCode,
                    old.TaxRateCode, old.TaxName, old.Rate, old.CalculationType, basis, old.TaxAmount, PurchaseTaxSource.Xml));
            }
        }
        // Allocate percentage VAT over the final cost bases, retaining each header rate total.
        foreach (var declared in header.TaxSummaries.Where(t => t.TaxCode == SriTaxCategoryCodes.Vat))
        {
            var members = invoice.Lines.Where(l => rows[l.Id].Any(t => t.TaxCode == SriTaxCategoryCodes.Vat
                && t.TaxRateCode == declared.TaxRateCode)).ToList();
            foreach (var line in members.Where(l => !l.IsXmlBreakdown))
            {
                var old = rows[line.Id].Single(t => t.TaxCode == SriTaxCategoryCodes.Vat);
                rows[line.Id].Remove(old);
                rows[line.Id].Add(PurchaseInvoiceDetailTax.Create(line.Id, line.TenantId, old.TaxCode,
                    old.TaxRateCode, old.TaxName, old.Rate, old.CalculationType, old.TaxableBase,
                    Money(old.TaxableBase * old.Rate!.Value / 100m), PurchaseTaxSource.Xml));
            }
            var residue = declared.TaxAmount - members.Sum(l => rows[l.Id].Single(t => t.TaxCode == SriTaxCategoryCodes.Vat).TaxAmount);
            if (residue == 0) continue;
            if (!RoundsTogether(declared.TaxAmount - residue, declared.TaxAmount, members.Count)) return Invalid;
            var target = members.Where(l => !l.IsXmlBreakdown && l.OperationalCostBase > 0)
                .OrderByDescending(l => l.OperationalCostBase).ThenBy(l => l.SortOrder).FirstOrDefault();
            if (target is null) return Invalid;
            var vat = rows[target.Id].Single(t => t.TaxCode == SriTaxCategoryCodes.Vat);
            if (vat.TaxAmount + residue < 0) return Invalid;
            rows[target.Id].Remove(vat);
            rows[target.Id].Add(PurchaseInvoiceDetailTax.Create(target.Id, target.TenantId, vat.TaxCode,
                vat.TaxRateCode, vat.TaxName, vat.Rate, vat.CalculationType, vat.TaxableBase,
                vat.TaxAmount + residue, PurchaseTaxSource.Xml));
        }
        if (Money(rows.Values.SelectMany(t => t).Where(t => t.TaxCode == SriTaxCategoryCodes.Vat).Sum(t => t.TaxableBase)
                - header.TaxSummaries.Where(t => t.TaxCode == SriTaxCategoryCodes.Ice).Sum(t => t.TaxAmount)) != xml.TotalWithoutTaxes)
            return Invalid;
        var planned = invoice.Lines.Sum(l => l.IsXmlBreakdown ? l.TaxInclusiveTotal
            : rows[l.Id].Single(t => t.TaxCode == SriTaxCategoryCodes.Vat).TaxableBase
                + rows[l.Id].Where(t => t.TaxCode != SriTaxCategoryCodes.Ice).Sum(t => t.TaxAmount));
        if (Money(planned) != total) return Invalid;
        foreach (var line in invoice.Lines.Where(l => !l.IsXmlBreakdown)) line.ReconcileXmlTaxes(rows[line.Id]);
        return null;
    }
}
