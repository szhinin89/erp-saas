namespace ERP.Application.Access.Caching;

/// <summary>
/// <see cref="Stamp"/> es null cuando no se puede publicar una entrada (fallo de caché o
/// invalidación en curso): el consumidor resuelve desde BD y no escribe.
/// </summary>
public sealed record PermissionsCacheReadResult(
    IReadOnlyList<string>? Keys,
    PermissionsCacheMissReason? MissReason,
    PermissionsCacheStamp? Stamp = null
);

/// <summary>
/// Versiones (tenant, empresa, usuario) observadas antes de cargar permisos de BD, y el instante
/// monotónico (<see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>) en que se observaron.
/// </summary>
public sealed record PermissionsCacheStamp(
    long TenantVersion,
    long CompanyVersion,
    long UserVersion,
    long ObservedAtTimestamp
);
