namespace ERP.Application.Common;

/// <summary>
/// Marca una petición MediatR cuyo resultado puede almacenarse en <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>.
/// La clave incluye el tenant actual (<see cref="ICurrentSubscriber"/>); invalidación explícita por comandos es opcional (TTL por defecto).
/// </summary>
public interface ICacheable
{
    /// <summary>Tiempo de vida absoluto en segundos.</summary>
    int CacheTTL { get; }
}
