using System.Globalization;
using System.Text.Json;
using ERP.Application.Admin;
using ERP.Domain.Configuration.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Config por tenant: todas las consultas a <c>Config*</c> usan <c>IgnoreQueryFilters</c> y filtran por
/// <c>tenantId</c> en el predicado para ser correctas con JWT SuperAdmin (<c>tenant_id</c> ambiente vacío).
/// </summary>
public sealed class ConfigService : IConfigService
{
    private readonly ErpDbContext _db;
    private readonly IMemoryCache _cache;
    private static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(20),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2),
    };

    public ConfigService(ErpDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task WarmupTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return;
        _ = await GetSnapshotAsync(tenantId, ct);
    }

    public async Task<ResolvedConfigValueDto?> GetValueAsync(
        Guid tenantId,
        string key,
        string? module = null,
        string? feature = null,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return null;
        var normalizedKey = NormalizeKey(key);
        if (normalizedKey.Length == 0) return null;

        var snapshot = await GetSnapshotAsync(tenantId, ct);

        var normalizedFeature = NormalizeScope(feature);
        if (normalizedFeature.Length > 0 &&
            snapshot.Feature.TryGetValue((normalizedFeature, normalizedKey), out var featureRow))
        {
            return new ResolvedConfigValueDto(tenantId, normalizedKey, NormalizeScopeOrNull(module), normalizedFeature, "feature", featureRow.Value, featureRow.DataType);
        }

        var normalizedModule = NormalizeScope(module);
        if (normalizedModule.Length > 0 &&
            snapshot.Module.TryGetValue((normalizedModule, normalizedKey), out var moduleRow))
        {
            return new ResolvedConfigValueDto(tenantId, normalizedKey, normalizedModule, NormalizeScopeOrNull(feature), "module", moduleRow.Value, moduleRow.DataType);
        }

        if (snapshot.Global.TryGetValue(normalizedKey, out var globalRow))
        {
            return new ResolvedConfigValueDto(tenantId, normalizedKey, NormalizeScopeOrNull(module), NormalizeScopeOrNull(feature), "global", globalRow.Value, globalRow.DataType);
        }

        _ = userId; // reservado para alcance usuario en siguiente etapa.
        return null;
    }

    public async Task<T?> GetValueTypedAsync<T>(
        Guid tenantId,
        string key,
        string? module = null,
        string? feature = null,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        var resolved = await GetValueAsync(tenantId, key, module, feature, userId, ct);
        if (resolved is null) return default;

        var parsed = ParseByDataType(resolved.Value, resolved.DataType);
        if (parsed is null) return default;

        if (parsed is T ok) return ok;

        if (typeof(T) == typeof(string))
            return (T)(object)resolved.Value;

        if (parsed is JsonDocument jd)
        {
            try
            {
                var typed = jd.RootElement.Deserialize<T>();
                return typed;
            }
            catch
            {
                return default;
            }
        }

        try
        {
            var converted = Convert.ChangeType(parsed, typeof(T), CultureInfo.InvariantCulture);
            return (T?)converted;
        }
        catch
        {
            return default;
        }
    }

    public async Task<IReadOnlyList<ConfigEntryDto>> ListGlobalAsync(Guid tenantId, CancellationToken ct = default)
    {
        var rows = await _db.ConfigGlobals.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Key)
            .ToListAsync(ct);
        return rows.Select(MapGlobal).ToList();
    }

    public async Task<IReadOnlyList<ConfigEntryDto>> ListModuleAsync(Guid tenantId, string module, CancellationToken ct = default)
    {
        var normalizedModule = NormalizeScope(module);
        var rows = await _db.ConfigModules.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Module == normalizedModule)
            .OrderBy(x => x.Key)
            .ToListAsync(ct);
        return rows.Select(MapModule).ToList();
    }

    public async Task<IReadOnlyList<ConfigEntryDto>> ListFeatureAsync(Guid tenantId, string feature, CancellationToken ct = default)
    {
        var normalizedFeature = NormalizeScope(feature);
        var rows = await _db.ConfigFeatures.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Feature == normalizedFeature)
            .OrderBy(x => x.Key)
            .ToListAsync(ct);
        return rows.Select(MapFeature).ToList();
    }

    public async Task<ConfigEntryDto> UpsertGlobalAsync(Guid tenantId, string key, string value, string dataType, Guid updatedBy, CancellationToken ct = default)
    {
        var normalizedKey = NormalizeKey(key);
        var normalizedType = NormalizeDataType(dataType);
        ValidateDataType(value, normalizedType);

        var row = await _db.ConfigGlobals.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Key == normalizedKey, ct);
        if (row is null)
        {
            row = ConfigGlobal.Create(tenantId, normalizedKey, value, normalizedType, updatedBy);
            await _db.ConfigGlobals.AddAsync(row, ct);
        }
        else
        {
            row.UpdateValue(value, normalizedType, updatedBy);
        }

        await _db.SaveChangesAsync(ct);
        InvalidateCache(tenantId);
        return MapGlobal(row);
    }

    public async Task<ConfigEntryDto> UpsertModuleAsync(Guid tenantId, string module, string key, string value, string dataType, Guid updatedBy, CancellationToken ct = default)
    {
        var normalizedModule = NormalizeScope(module);
        var normalizedKey = NormalizeKey(key);
        var normalizedType = NormalizeDataType(dataType);
        ValidateDataType(value, normalizedType);

        var row = await _db.ConfigModules.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Module == normalizedModule && x.Key == normalizedKey, ct);
        if (row is null)
        {
            row = ConfigModule.Create(tenantId, normalizedModule, normalizedKey, value, normalizedType, updatedBy);
            await _db.ConfigModules.AddAsync(row, ct);
        }
        else
        {
            row.UpdateValue(value, normalizedType, updatedBy);
        }

        await _db.SaveChangesAsync(ct);
        InvalidateCache(tenantId);
        return MapModule(row);
    }

    public async Task<ConfigEntryDto> UpsertFeatureAsync(Guid tenantId, string feature, string key, string value, string dataType, Guid updatedBy, CancellationToken ct = default)
    {
        var normalizedFeature = NormalizeScope(feature);
        var normalizedKey = NormalizeKey(key);
        var normalizedType = NormalizeDataType(dataType);
        ValidateDataType(value, normalizedType);

        var row = await _db.ConfigFeatures.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Feature == normalizedFeature && x.Key == normalizedKey, ct);
        if (row is null)
        {
            row = ConfigFeature.Create(tenantId, normalizedFeature, normalizedKey, value, normalizedType, updatedBy);
            await _db.ConfigFeatures.AddAsync(row, ct);
        }
        else
        {
            row.UpdateValue(value, normalizedType, updatedBy);
        }

        await _db.SaveChangesAsync(ct);
        InvalidateCache(tenantId);
        return MapFeature(row);
    }

    public async Task<bool> DeleteGlobalAsync(Guid tenantId, string key, CancellationToken ct = default)
    {
        var normalizedKey = NormalizeKey(key);
        var row = await _db.ConfigGlobals.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Key == normalizedKey, ct);
        if (row is null) return false;

        _db.ConfigGlobals.Remove(row);
        await _db.SaveChangesAsync(ct);
        InvalidateCache(tenantId);
        return true;
    }

    public async Task<bool> DeleteModuleAsync(Guid tenantId, string module, string key, CancellationToken ct = default)
    {
        var normalizedModule = NormalizeScope(module);
        var normalizedKey = NormalizeKey(key);
        var row = await _db.ConfigModules.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Module == normalizedModule && x.Key == normalizedKey, ct);
        if (row is null) return false;

        _db.ConfigModules.Remove(row);
        await _db.SaveChangesAsync(ct);
        InvalidateCache(tenantId);
        return true;
    }

    public async Task<bool> DeleteFeatureAsync(Guid tenantId, string feature, string key, CancellationToken ct = default)
    {
        var normalizedFeature = NormalizeScope(feature);
        var normalizedKey = NormalizeKey(key);
        var row = await _db.ConfigFeatures.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Feature == normalizedFeature && x.Key == normalizedKey, ct);
        if (row is null) return false;

        _db.ConfigFeatures.Remove(row);
        await _db.SaveChangesAsync(ct);
        InvalidateCache(tenantId);
        return true;
    }

    private async Task<TenantConfigSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken ct)
    {
        var cacheKey = CacheKey(tenantId);
        if (_cache.TryGetValue<TenantConfigSnapshot>(cacheKey, out var hit) && hit is not null)
            return hit;

        var globals = await _db.ConfigGlobals.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(ct);
        var modules = await _db.ConfigModules.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(ct);
        var features = await _db.ConfigFeatures.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(ct);

        var snapshot = new TenantConfigSnapshot
        {
            Global = globals.ToDictionary(x => x.Key, x => new CacheValue(x.Value, x.DataType), StringComparer.OrdinalIgnoreCase),
            Module = modules.ToDictionary(x => (x.Module, x.Key), x => new CacheValue(x.Value, x.DataType)),
            Feature = features.ToDictionary(x => (x.Feature, x.Key), x => new CacheValue(x.Value, x.DataType)),
        };

        _cache.Set(cacheKey, snapshot, CacheOptions);
        return snapshot;
    }

    private void InvalidateCache(Guid tenantId) => _cache.Remove(CacheKey(tenantId));
    private static string CacheKey(Guid tenantId) => $"cfg:{tenantId:N}";

    private static string NormalizeKey(string key) => (key ?? string.Empty).Trim().ToLowerInvariant();
    private static string NormalizeScope(string? scope) => (scope ?? string.Empty).Trim().ToLowerInvariant();
    private static string? NormalizeScopeOrNull(string? scope)
    {
        var n = NormalizeScope(scope);
        return n.Length == 0 ? null : n;
    }
    private static string NormalizeDataType(string dataType) => (dataType ?? "string").Trim().ToLowerInvariant();

    private static object? ParseByDataType(string value, string dataType)
    {
        var dt = NormalizeDataType(dataType);
        return dt switch
        {
            "string" => value,
            "bool" or "boolean" => bool.TryParse(value, out var b) ? b : null,
            "int" or "int32" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null,
            "long" or "int64" => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : null,
            "decimal" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null,
            "double" => double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var db) ? db : null,
            "datetime" => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dtm) ? dtm : null,
            "json" => JsonDocument.Parse(value),
            _ => value,
        };
    }

    private static void ValidateDataType(string value, string dataType)
    {
        try
        {
            var parsed = ParseByDataType(value, dataType);
            if (parsed is null)
                throw new ArgumentException($"El valor no coincide con el dataType '{dataType}'.");
        }
        catch (JsonException)
        {
            throw new ArgumentException($"El valor no coincide con el dataType '{dataType}'.");
        }
    }

    private static ConfigEntryDto MapGlobal(ConfigGlobal x) =>
        new(x.Id, x.TenantId, "global", null, null, x.Key, x.Value, x.DataType, x.UpdatedAt, x.UpdatedBy);

    private static ConfigEntryDto MapModule(ConfigModule x) =>
        new(x.Id, x.TenantId, "module", x.Module, null, x.Key, x.Value, x.DataType, x.UpdatedAt, x.UpdatedBy);

    private static ConfigEntryDto MapFeature(ConfigFeature x) =>
        new(x.Id, x.TenantId, "feature", null, x.Feature, x.Key, x.Value, x.DataType, x.UpdatedAt, x.UpdatedBy);

    private sealed class TenantConfigSnapshot
    {
        public Dictionary<string, CacheValue> Global { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<(string Module, string Key), CacheValue> Module { get; init; } = new();
        public Dictionary<(string Feature, string Key), CacheValue> Feature { get; init; } = new();
    }

    private sealed record CacheValue(string Value, string DataType);
}

