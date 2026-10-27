namespace ERP.Application.Access.Caching;

/// <summary>
/// Invalidación centralizada del cache de permisos efectivos (write-side exclusivo).
/// Los handlers de mutación deben usar esta abstracción — nunca <see cref="IPermissionsCacheService"/> —
/// y, en la práctica, a través de <see cref="PermissionsCacheInvalidation.RunAsync{T}"/>.
/// <para>
/// SEC-06 — protocolo en dos fases, fail-closed:
/// </para>
/// <list type="number">
/// <item><see cref="TryBeginAsync"/> ANTES de persistir: marca el alcance como "invalidación en
/// curso"; mientras dure, ninguna lectura usa ni publica entradas de ese alcance (resuelve desde BD).
/// Si devuelve <c>false</c> no se puede garantizar la invalidación y NO se debe persistir.</item>
/// <item><see cref="TryCompleteAsync"/> DESPUÉS de persistir: incrementa atómicamente la versión del
/// alcance y retira la marca. Si devuelve <c>false</c> la marca permanece (expira por sí sola cuando
/// ya no puede quedar ninguna entrada obsoleta), así que tampoco se reutilizan permisos antiguos.</item>
/// </list>
/// </summary>
public interface IPermissionsCacheInvalidator
{
    Task<bool> TryBeginAsync(
        PermissionsCacheScope scope,
        CancellationToken cancellationToken = default
    );

    Task<bool> TryCompleteAsync(
        PermissionsCacheScope scope,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Alcance de una invalidación: todo un tenant, una empresa o un usuario en una empresa.</summary>
public sealed record PermissionsCacheScope
{
    private PermissionsCacheScope(string key) => Key = key;

    /// <summary>Identificador estable del alcance (sin datos personales).</summary>
    public string Key { get; }

    public static PermissionsCacheScope Tenant(Guid tenantId) => new($"tenant:{tenantId:N}");

    public static PermissionsCacheScope Company(Guid companyId) => new($"company:{companyId:N}");

    public static PermissionsCacheScope User(Guid companyId, Guid userId) =>
        new($"user:{companyId:N}:{userId:N}");

    public override string ToString() => Key;
}
