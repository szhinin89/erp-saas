using ERP.Domain.Modules.SriCatalogs.Enums;

namespace ERP.Application.Common.Services;

public sealed record TaxRateResult(decimal Rate, string Name);

/// <summary>
/// FLOW-READY-02F.1 — proyección completa de una fila de catálogo SRI (IVA/ICE/IRBPNR), usada por
/// el modelo de impuestos múltiples por línea de compra (<c>PurchaseInvoiceDetailTax</c>). A
/// diferencia de <see cref="TaxRateResult"/> (solo tarifa+nombre, usado por los campos escalares
/// legacy de IVA/ICE), esta proyección también expone <see cref="CalculationType"/> — necesario
/// porque ICE/IRBPNR pueden ser porcentuales o de monto fijo por unidad.
/// </summary>
public sealed record SriTaxCatalogEntry(
    string Code,
    string Name,
    decimal? Percentage,
    decimal? UnitValue,
    SriTaxCalculationType CalculationType
);

public interface ISriTaxResolver
{
    Task<decimal?> GetVatRateAsync(string vatCode, CancellationToken ct = default);
    Task<decimal?> GetIceRateAsync(string iceCode, CancellationToken ct = default);

    Task<TaxRateResult?> GetVatRateWithNameAsync(string vatCode, CancellationToken ct = default);
    Task<TaxRateResult?> GetIceRateWithNameAsync(string iceCode, CancellationToken ct = default);

    Task<string?> GetPaymentMethodNameAsync(string code, CancellationToken ct = default);

    // ── FLOW-READY-02F.1 — modelo de impuestos múltiples por línea de compra ────────────────
    // Miembros aditivos: no reemplazan los anteriores (Sales sigue usando GetVatRateWithNameAsync/
    // GetIceRateWithNameAsync sin ningún cambio de comportamiento).
    Task<SriTaxCatalogEntry?> GetVatCatalogEntryAsync(string vatCode, CancellationToken ct = default);
    Task<SriTaxCatalogEntry?> GetIceCatalogEntryAsync(string iceCode, CancellationToken ct = default);
    Task<SriTaxCatalogEntry?> GetIrbpnrCatalogEntryAsync(
        string irbpnrCode,
        CancellationToken ct = default
    );
}
