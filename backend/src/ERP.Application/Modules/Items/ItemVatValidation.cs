using ERP.Application.Common;

namespace ERP.Application.Items;

internal static class ItemVatValidation
{
    /// <param name="today">Fecha de negocio de la empresa — resuelta por el llamador vía ICompanyClock.</param>
    public static async Task<string?> ValidateAsync(ISriCatalogResolver sri, string? saleCode, string? purchaseCode,
        DateOnly today, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(saleCode)) return "El IVA de venta es obligatorio.";
        if (string.IsNullOrWhiteSpace(purchaseCode)) return "El IVA de compra es obligatorio.";
        var rates = await sri.ResolveVatRatesAsync([saleCode.Trim(), purchaseCode.Trim()], ct);
        if (!rates.TryGetValue(saleCode.Trim(), out var sale) || !sale.IsEffectiveOn(today))
            return "El IVA de venta no existe o no está vigente en el catálogo SRI.";
        if (!rates.TryGetValue(purchaseCode.Trim(), out var purchase) || !purchase.IsEffectiveOn(today))
            return "El IVA de compra no existe o no está vigente en el catálogo SRI.";
        return null;
    }
}
