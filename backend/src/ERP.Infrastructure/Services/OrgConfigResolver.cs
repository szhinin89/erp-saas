using ERP.Application.Common;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using System.Globalization;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Implementación del resolver centralizado de configuraciones organizacionales.
/// Inyecta ICurrentTenant y ICurrentCompany para obtener el contexto de ejecución
/// sin exponerlo en la interfaz pública.
/// </summary>
public sealed class OrgConfigResolver : IOrgConfigResolver
{
    private readonly IOrgSettingsRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public OrgConfigResolver(
        IOrgSettingsRepository repo,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<string?> GetValueAsync(
        OrgScope scope, Guid scopeId, string key, CancellationToken ct = default)
    {
        var setting = await _repo.GetAsync(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            scope, scopeId, key, ct);

        return setting?.Value;
    }

    public async Task<T?> GetValueAsync<T>(
        OrgScope scope, Guid scopeId, string key, CancellationToken ct = default)
    {
        var raw = await GetValueAsync(scope, scopeId, key, ct);
        if (raw is null)
            return default;

        return ConvertValue<T>(raw);
    }

    private static T? ConvertValue<T>(string raw)
    {
        var targetType = typeof(T);
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying == typeof(string))
            return (T)(object)raw;

        if (underlying == typeof(Guid))
            return Guid.TryParse(raw, out var g) ? (T)(object)g : default;

        if (underlying == typeof(int))
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
                ? (T)(object)i : default;

        if (underlying == typeof(decimal))
            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
                ? (T)(object)d : default;

        if (underlying == typeof(bool))
            return bool.TryParse(raw, out var b) ? (T)(object)b : default;

        throw new NotSupportedException($"OrgConfigResolver: tipo '{underlying.Name}' no está soportado.");
    }
}
