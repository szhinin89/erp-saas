using ERP.Domain.Modules.Purchases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;

namespace ERP.Application.Modules.Purchases.UseCases;

internal static class CreditNoteReturnLines
{
    internal static async Task<(List<PurchaseReturn.DraftLineInput> Returns,
        List<PurchaseCreditNote.DraftLineInput> Fiscal, string? Error)> ResolveAsync(
        PurchaseInvoice invoice, IReadOnlyList<PurchaseReturnDraftLineInput> inputs,
        IPurchaseReturnRepository repository, Guid tenantId, CancellationToken ct)
    {
        var returns = new List<PurchaseReturn.DraftLineInput>();
        var fiscal = new List<PurchaseCreditNote.DraftLineInput>();
        if (inputs.Count == 0 || inputs.Any(l => l.Quantity <= 0 || decimal.Round(l.Quantity, 4) != l.Quantity))
            return (returns, fiscal, "Indique cantidades a devolver mayores a cero, con hasta cuatro decimales.");
        if (inputs.Select(l => l.OriginalInvoiceDetailId).Distinct().Count() != inputs.Count)
            return (returns, fiscal, "No se puede repetir una línea de la factura.");
        var returned = await repository.GetReturnedQuantitiesByInvoiceDetailIdsAsync(
            tenantId, inputs.Select(l => l.OriginalInvoiceDetailId).ToList(), ct);
        foreach (var input in inputs)
        {
            var source = invoice.Lines.FirstOrDefault(l => l.Id == input.OriginalInvoiceDetailId);
            if (source?.ItemId is null || source.Quantity <= 0)
                return (returns, fiscal, "El producto indicado no pertenece a la factura afectada.");
            var warehouse = source.WarehouseId ?? invoice.GlobalWarehouseId;
            if (warehouse is null)
                return (returns, fiscal, "El producto no tiene una bodega de origen.");
            var available = source.Quantity - returned.GetValueOrDefault(source.Id);
            if (input.Quantity > available)
                return (returns, fiscal, $"La cantidad a devolver de '{source.Description}' excede lo disponible ({available}).");
            var vat = source.Taxes.FirstOrDefault(t => t.TaxCode == SriTaxCategoryCodes.Vat);
            if (vat is null)
                return (returns, fiscal, $"La línea '{source.Description}' no tiene un snapshot de IVA registrado.");
            decimal Prorate(decimal amount) => PurchaseReturn.ProrateAmount(amount, input.Quantity, source.Quantity);
            decimal Tax(string code) => source.Taxes.Where(t => t.TaxCode == code).Sum(t => Prorate(t.TaxAmount));
            returns.Add(new(source.Id, source.ItemId.Value, input.Quantity, warehouse.Value));
            fiscal.Add(new(source.Description, Prorate(source.LineSubtotal - source.DiscountAmount),
                vat.TaxRateCode, vat.Rate, Tax(SriTaxCategoryCodes.Vat), source.Id, input.Quantity,
                Tax(SriTaxCategoryCodes.Ice), Tax(SriTaxCategoryCodes.Irbpnr)));
        }
        return (returns, fiscal, null);
    }
}
