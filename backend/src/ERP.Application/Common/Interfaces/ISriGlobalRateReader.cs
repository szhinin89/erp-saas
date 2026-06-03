namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Lectura de tarifas regulatorias desde global.sri_vat_rate y global.sri_ice_rate.
/// Reemplaza ITaxRateRepository — las tasas ya no son per-subscriber.
/// </summary>
public interface ISriGlobalRateReader
{
    /// <summary>Porcentaje IVA para el código SRI dado ("0","8","10", etc.). Null si no existe.</summary>
    Task<decimal?> GetVatPercentageAsync(string code, CancellationToken ct = default);

    /// <summary>Porcentaje ICE (o valor unitario) para el código SRI dado. Null si no existe.</summary>
    Task<decimal?> GetIcePercentageAsync(string code, CancellationToken ct = default);

    /// <summary>Porcentaje de un código de retención SRI ("303","721", etc.). Null si no existe.</summary>
    Task<decimal?> GetRetentionPercentageAsync(string code, CancellationToken ct = default);
}
