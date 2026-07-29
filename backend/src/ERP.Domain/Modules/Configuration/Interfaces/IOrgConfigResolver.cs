using ERP.Domain.Configuration.Enums;

namespace ERP.Domain.Configuration.Interfaces;

/// <summary>
/// Resolvedor centralizado de configuraciones organizacionales.
/// Es el único punto de acceso que deben usar los módulos del ERP para
/// leer configuraciones. Nunca consultar org_settings directamente.
/// El tenant y company se obtienen del contexto de ejecución actual.
/// </summary>
public interface IOrgConfigResolver
{
    /// <summary>Retorna el valor raw (string) o null si no está configurado.</summary>
    Task<string?> GetValueAsync(
        OrgScope scope,
        Guid scopeId,
        string key,
        CancellationToken ct = default
    );

    /// <summary>
    /// Retorna el valor convertido al tipo T, o default(T) si no está configurado.
    /// Tipos soportados: string, Guid, int, decimal, bool.
    /// </summary>
    Task<T?> GetValueAsync<T>(
        OrgScope scope,
        Guid scopeId,
        string key,
        CancellationToken ct = default
    );
}
