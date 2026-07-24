namespace ERP.Domain.Common;

/// <summary>
/// Generador de IDs distribuidos (41-bit timestamp, 10-bit worker, 12-bit sequence).
/// Sin secuencias PostgreSQL.
/// </summary>
public interface ISnowflakeIdGenerator
{
    long NextId();
}
