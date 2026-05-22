using ERP.Application.Access.Caching;

namespace ERP.Infrastructure.Access.Caching;

/// <summary>
/// Superficie interna de infraestructura (read/write + invalidación).
/// Application consume <see cref="IPermissionsCacheService"/> e <see cref="IPermissionsCacheInvalidator"/> por separado.
/// </summary>
public interface IPermissionsCacheBackend : IPermissionsCacheService, IPermissionsCacheInvalidator;
