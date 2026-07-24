namespace ERP.Application.Common;

public sealed record SriUomInfo(string Abbrev, string Name);
public sealed record SriVatInfo(string Name, decimal Percent);
public sealed record SriIceInfo(string Name, decimal? Percent);

/// <summary>
/// Resuelve códigos SRI a información descriptiva lista para mostrar en la UI.
/// Patrón: la base de datos almacena códigos; los DTOs de lectura devuelven nombres/abreviaturas.
/// </summary>
public interface ISriCatalogResolver
{
    /// <summary>Batch: carga todos los UOM de los códigos proporcionados en una sola query.</summary>
    Task<IReadOnlyDictionary<string, SriUomInfo>> ResolveUomsAsync(
        IEnumerable<string> codes, CancellationToken ct = default);

    /// <summary>Batch: carga todos los IVA de los códigos proporcionados en una sola query.</summary>
    Task<IReadOnlyDictionary<string, SriVatInfo>> ResolveVatRatesAsync(
        IEnumerable<string> codes, CancellationToken ct = default);

    /// <summary>Batch: carga todos los ICE de los códigos proporcionados en una sola query.</summary>
    Task<IReadOnlyDictionary<string, SriIceInfo>> ResolveIceRatesAsync(
        IEnumerable<string> codes, CancellationToken ct = default);
}
