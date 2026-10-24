using ERP.Domain.Modules.Purchases;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Application.Modules.Purchases.Services;

public static class PurchaseXmlBreakdown
{
    public static async Task<string?> ApplyAsync(PurchaseInvoiceDetail line,
        PurchaseXmlAllocationInput allocation, IPurchaseReceptionDocumentRepository receptions,
        ISriTaxResolver resolver, CancellationToken ct)
    {
        if (line.PurchaseReceptionLineId is not { } id || line.ItemId is null
            || allocation.Taxes is null || allocation.Taxes.Count == 0
            || allocation.Taxes.Select(t => t.PurchaseReceptionLineTaxId).Distinct().Count() != allocation.Taxes.Count)
            return "El desglose requiere producto, origen XML e impuestos sin duplicados.";
        var document = await receptions.GetByLineIdAsync(line.TenantId, id, ct);
        var original = document?.Lines.SingleOrDefault(l => l.Id == id);
        if (original is null || original.TenantId != line.TenantId)
            return "No se encuentra la línea fiscal del desglose.";
        var taxes = new List<PurchaseInvoiceDetailTax>();
        foreach (var input in allocation.Taxes)
        {
            var source = original.Taxes.SingleOrDefault(t => t.Id == input.PurchaseReceptionLineTaxId);
            if (source is null || input.TaxableBase < 0 || input.TaxAmount < 0)
                return "El impuesto asignado no pertenece a la línea fiscal o tiene importes inválidos.";
            var entry = source.TaxCode switch
            {
                SriTaxCategoryCodes.Vat => await resolver.GetVatCatalogEntryAsync(source.TaxRateCode, ct),
                SriTaxCategoryCodes.Ice => await resolver.GetXmlIceCatalogEntryAsync(source.TaxRateCode, ct),
                SriTaxCategoryCodes.Irbpnr => await resolver.GetIrbpnrCatalogEntryAsync(source.TaxRateCode, ct),
                _ => null,
            };
            if (entry is null)
                return "El impuesto XML del desglose no está disponible en el catálogo SRI.";
            taxes.Add(PurchaseInvoiceDetailTax.Create(line.Id, line.TenantId, source.TaxCode,
                source.TaxRateCode, entry.Name, source.Tarifa, entry.CalculationType,
                input.TaxableBase, input.TaxAmount, PurchaseTaxSource.XmlAllocation, source.Id));
        }
        try { line.ApplyXmlAllocation(allocation.Subtotal, allocation.Discount, taxes); }
        catch (DomainRuleViolationException ex) { return ex.Message; }
        return null;
    }

    public static async Task<string?> ValidateAsync(PurchaseInvoice invoice,
        IReadOnlyList<PurchaseInvoiceDetail> lines, IPurchaseReceptionDocumentRepository receptions,
        CancellationToken ct)
    {
        foreach (var group in lines.Where(l => l.PurchaseReceptionLineId.HasValue)
            .GroupBy(l => l.PurchaseReceptionLineId!.Value).Where(g => g.Count() > 1 || g.Any(l => l.IsXmlBreakdown)))
        {
            var source = await receptions.GetByLineIdAsync(invoice.TenantId, group.Key, ct);
            if (source is null || source.TenantId != invoice.TenantId || source.CompanyId != invoice.CompanyId
                || source.BranchId != invoice.BranchId || source.SupplierRuc != invoice.SupplierTaxId
                || (source.SupplierId.HasValue && source.SupplierId != invoice.SupplierId)
                || source.InvoiceNumber != invoice.InvoiceNumber || source.IssueDate != invoice.IssueDate
                || source.CurrencyCode != invoice.CurrencyCode
                || (source.PurchaseId.HasValue && source.PurchaseId != invoice.Id)
                || (!string.IsNullOrWhiteSpace(invoice.AccessKey) && source.AccessKey != invoice.AccessKey))
                return "El origen del desglose no corresponde a esta compra, empresa o sucursal.";
            var original = source.Lines.SingleOrDefault(l => l.Id == group.Key);
            if (original is null || group.Any(l => l.InvoiceId != invoice.Id)
                || !Matches(group.ToList(), original))
                return "El desglose no cuadra exactamente con la línea XML por base, descuento e impuesto.";
        }
        return null;
    }

    public static bool Matches(IReadOnlyList<PurchaseInvoiceDetail> lines, PurchaseReceptionLine original)
    {
        if (lines.Count < 2 || lines.Any(l => !l.IsXmlBreakdown || l.ItemId is null
                || l.PurchaseReceptionLineId != original.Id || l.TenantId != original.TenantId)
            || lines.Select(l => l.Id).Distinct().Count() != lines.Count
            || lines.Sum(l => l.LineSubtotal) != original.LineSubtotal + original.Discount
            || lines.Sum(l => l.DiscountAmount) != original.Discount
            || lines.Sum(l => l.TaxableBase) != original.LineSubtotal
            || lines.Sum(l => l.TaxInclusiveTotal) != original.TotalLine)
            return false;
        var allocated = lines.SelectMany(l => l.Taxes).ToList();
        if (allocated.Any(t => t.Source != PurchaseTaxSource.XmlAllocation
            || !original.Taxes.Any(o => o.Id == t.PurchaseReceptionLineTaxId
                && o.TaxCode == t.TaxCode && o.TaxRateCode == t.TaxRateCode && o.Tarifa == t.Rate)))
            return false;
        return original.Taxes.Count > 0 && original.Taxes.All(o =>
            allocated.Any(t => t.PurchaseReceptionLineTaxId == o.Id)
            && allocated.Where(t => t.PurchaseReceptionLineTaxId == o.Id).Sum(t => t.TaxableBase) == o.TaxableBase
            && allocated.Where(t => t.PurchaseReceptionLineTaxId == o.Id).Sum(t => t.TaxAmount) == o.TaxAmount);
    }
}
