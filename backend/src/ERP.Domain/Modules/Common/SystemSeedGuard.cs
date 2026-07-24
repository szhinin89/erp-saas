using ERP.Domain.Exceptions;

namespace ERP.Domain.Common;

/// <summary>
/// Único punto de aplicación de la protección de registros sembrados por el sistema
/// (<see cref="ISystemSeeded"/>). Reemplaza cualquier comparación ad hoc tipo
/// <c>if (Code == "NO_APLICA")</c> dentro de una entidad — ver política de Bootstrap en
/// <c>CLAUDE.md</c>.
/// </summary>
public static class SystemSeedGuard
{
    /// <summary>
    /// Lanza <see cref="SystemSeededRecordException"/> si <paramref name="entity"/> fue sembrado por
    /// el Bootstrap del sistema. Se invoca explícitamente desde el método de dominio que representa la
    /// mutación a proteger (p. ej. <c>Disable()</c>) — nunca de forma implícita ni centralizada, para
    /// que cada entidad conserve el control de qué operaciones son realmente destructivas para ella.
    /// </summary>
    public static void EnsureEditable(this ISystemSeeded entity, string entityLabel, string action)
    {
        if (entity.IsSystemSeeded)
            throw new SystemSeededRecordException(entityLabel, action);
    }
}
