using System.Collections.Concurrent;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.Caching;

public sealed class EffectivePermissionKeysProvider : IEffectivePermissionKeysProvider
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> StampedeLocks = new();
    private static readonly TimeSpan StampedeLockWait = TimeSpan.FromMilliseconds(500);

    private readonly IPermissionsCacheService _cache;
    private readonly IPermissionsCacheDiagnostics _diagnostics;
    private readonly IAccessRepository _repo;

    public EffectivePermissionKeysProvider(
        IPermissionsCacheService cache,
        IPermissionsCacheDiagnostics diagnostics,
        IAccessRepository repo
    )
    {
        _cache = cache;
        _diagnostics = diagnostics;
        _repo = repo;
    }

    public async Task<IReadOnlyList<string>> GetAllowedKeysAsync(
        Guid tenantId,
        Guid companyId,
        Guid userId,
        Guid profileId,
        CancellationToken cancellationToken = default
    )
    {
        var cached = await _cache.ReadAsync(
            tenantId,
            companyId,
            userId,
            profileId,
            cancellationToken
        );
        if (cached.Keys is not null)
        {
            _diagnostics.RecordHit();
            return cached.Keys;
        }

        var sem = StampedeLocks.GetOrAdd(
            StampedeLockKey(companyId, userId),
            _ => new SemaphoreSlim(1, 1)
        );
        var acquired = await sem.WaitAsync(StampedeLockWait, cancellationToken);
        if (!acquired)
        {
            _diagnostics.RecordMiss(PermissionsCacheMissReason.StampedeFallback);
            return await LoadFromDatabaseAsync(tenantId, companyId, profileId, cancellationToken);
        }

        try
        {
            // SEC-06: esta lectura observa las versiones ANTES de cargar la BD; su sello es el único
            // con el que se publica la entrada (una carga anterior a una invalidación nace obsoleta).
            cached = await _cache.ReadAsync(
                tenantId,
                companyId,
                userId,
                profileId,
                cancellationToken
            );
            if (cached.Keys is not null)
            {
                _diagnostics.RecordHit();
                return cached.Keys;
            }

            _diagnostics.RecordMiss(cached.MissReason ?? PermissionsCacheMissReason.NotFound);

            var allowed = await LoadFromDatabaseAsync(
                tenantId,
                companyId,
                profileId,
                cancellationToken
            );

            // Sin sello (fallo de caché o invalidación en curso) se resuelve desde BD sin publicar.
            if (cached.Stamp is null)
                return allowed;

            await _cache.WriteAsync(
                tenantId,
                companyId,
                userId,
                profileId,
                allowed,
                cached.Stamp,
                cancellationToken
            );
            _diagnostics.RecordSet();
            return allowed;
        }
        finally
        {
            sem.Release();
        }
    }

    // SEC-05: solo concede permisos un perfil activo de la misma empresa de la membership; un
    // perfil desactivado o ajeno resuelve a vacío.
    private async Task<IReadOnlyList<string>> LoadFromDatabaseAsync(
        Guid tenantId,
        Guid companyId,
        Guid profileId,
        CancellationToken cancellationToken
    )
    {
        var items = await _repo.GetActiveProfilePermissionsForCompanyAsync(
            tenantId,
            companyId,
            profileId,
            cancellationToken
        );
        var allowed = items
            .Where(p => p.IsAllowed)
            .Select(p => p.PermissionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return allowed;
    }

    private static string StampedeLockKey(Guid companyId, Guid userId) =>
        $"{companyId:N}:{userId:N}";
}
