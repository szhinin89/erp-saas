namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Datos de contexto compartidos por todos los <see cref="ICompanyBootstrapStep"/> de una corrida
/// de bootstrap. Los steps se comunican entre sí exclusivamente a través del estado ya persistido
/// en base de datos (consultado por el step que lo necesite) — nunca a través de campos adicionales
/// de este contexto ni de referencias directas entre steps.
/// </summary>
public sealed record CompanyBootstrapContext(Guid TenantId, Guid CompanyId, Guid ActorId)
{
    /// <summary>
    /// Política del llamador (no comunicación entre steps): true solo cuando se aprovisiona una
    /// empresa NUEVA (alta de empresa o First Run). La auto-sanación del login y cualquier
    /// bootstrap sobre una empresa existente la dejan en false y no reciben perfiles iniciales.
    /// Es una propiedad init, no parte del constructor posicional, para no alterar la
    /// deconstrucción (tenantId, companyId, actorId) que usan los demás steps.
    /// </summary>
    public bool SeedInitialProfiles { get; init; }
}

/// <summary>
/// Unidad de inicialización de un único dominio/módulo dentro del bootstrap de una empresa nueva.
/// Implementaciones registradas en DI como <c>ICompanyBootstrapStep</c> (registro múltiple) y
/// descubiertas/ordenadas/ejecutadas exclusivamente por <c>CompanyBootstrapOrchestrator</c>
/// (implementación de <see cref="ICompanyBootstrapService"/>) — ningún otro componente debe
/// invocar un step directamente.
///
/// Reglas de un step:
///   - Solo crea datos de su propio dominio (no de otros módulos).
///   - Es idempotente: seguro de ejecutar más de una vez sobre la misma empresa.
///   - Si necesita datos creados por un step anterior, los consulta desde la base de datos
///     (nunca depende de detalles internos de la clase de ese otro step) y asume que ya se
///     ejecutó, según el orden declarado por <see cref="Order"/>.
/// </summary>
public interface ICompanyBootstrapStep
{
    /// <summary>
    /// Orden de ejecución explícito, ascendente. No depende del orden de registro en el
    /// contenedor DI — el orquestador siempre ordena por este valor antes de ejecutar.
    /// </summary>
    int Order { get; }

    Task ExecuteAsync(
        CompanyBootstrapContext context,
        CancellationToken cancellationToken = default
    );
}
