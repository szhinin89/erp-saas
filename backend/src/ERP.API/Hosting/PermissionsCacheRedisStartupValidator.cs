using ERP.Infrastructure.Access.Caching;

namespace ERP.API.Hosting;

/// <summary>
/// SEC-06 (opción A) — con Redis configurado, este es el Redis de la caché de permisos: el host
/// (API y worker, ambos vía <see cref="ErpHostRegistration"/>) no arranca si no cumple
/// <see cref="PermissionsCacheRedisGuarantee"/> o si no se puede verificar. Solo se registra con
/// Redis configurado; sin Redis (instancia única) la caché es local.
/// </summary>
public sealed class PermissionsCacheRedisStartupValidator(IConfiguration configuration)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        PermissionsCacheRedisGuarantee.EnsureAsync(
            ErpHostRegistration.RedisConnection(configuration)
                ?? throw new InvalidOperationException("SEC-06: Redis no configurado."),
            cancellationToken
        );

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
