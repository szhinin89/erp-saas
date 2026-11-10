namespace ERP.Domain.Modules.Sales.Interfaces;

/// <summary>
/// SALES-PREFERENCES-INTEGRITY-01: regla única de validez de los defaults de factura de venta
/// de la empresa (ver docs/architecture/frozen-infrastructure.md § Valores por Defecto de
/// Facturación). La usan la escritura (validador del comando, rechaza), la lectura
/// administrativa (advierte) e <see cref="IInvoiceDefaultsResolver"/> (descarta y advierte);
/// ninguno replica estas condiciones por su cuenta.
/// </summary>
public interface IInvoiceDefaultsCatalogValidator
{
    /// <summary>
    /// Códigos de tipo de documento SRI aceptados hoy como default de ventas (política vigente:
    /// solo <c>01</c> Factura, si está activo en el catálogo). La UI filtra su selector con esta
    /// lista — no replica la regla.
    /// </summary>
    Task<IReadOnlyList<string>> GetAllowedDocTypeCodesAsync(CancellationToken ct = default);

    /// <summary>True si <paramref name="code"/> está en <see cref="GetAllowedDocTypeCodesAsync"/>.</summary>
    Task<bool> IsValidDocTypeCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Forma de pago SRI existente y activa.</summary>
    Task<bool> IsValidSriPaymentMethodCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Condición de pago existente, activa y del tenant.</summary>
    Task<bool> IsValidPaymentTermAsync(
        Guid tenantId,
        Guid paymentTermId,
        CancellationToken ct = default
    );
}

/// <summary>Mensajes únicos de <see cref="IInvoiceDefaultsCatalogValidator"/>.</summary>
public static class InvoiceDefaultsCatalogMessages
{
    public const string InvalidDocType =
        "El tipo de documento predeterminado debe ser Factura (01) activa en el catálogo SRI.";
    public const string InvalidSriPaymentMethod =
        "La forma de pago SRI predeterminada no existe o está inactiva.";
    public const string InvalidPaymentTerm =
        "La condición de pago predeterminada no existe, está inactiva o no pertenece a esta organización.";

    public const string StoredValueIgnoredSuffix =
        " El valor guardado se ignora hasta que se corrija en Configuración → Ventas.";
}
