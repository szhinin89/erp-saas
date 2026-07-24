namespace ERP.Infrastructure.Seeding.Global;

/// <summary>
/// Equivalente de <c>ICompanyBootstrapStep</c> a nivel de instalación: unidad de inicialización de
/// un único mecanismo de datos globales (sin <c>TenantId</c>/<c>CompanyId</c>), ejecutada una vez
/// por arranque de la API. Descubierta/ordenada/ejecutada exclusivamente por
/// <see cref="GlobalBootstrapOrchestrator"/> — ningún otro componente debe invocar un step
/// directamente.
///
/// No incluye aquí la aplicación de migraciones EF (<c>Database.MigrateAsync()</c>) ni los
/// catálogos sembrados por <c>HasData()</c>: ambos son responsabilidad del motor de migraciones,
/// se aplican antes de que exista un <see cref="ErpDbContext"/> con esquema utilizable, y por
/// tanto son un prerrequisito del bootstrap global, no un step de él — mismo rol que cumple la
/// creación de <c>Company</c> respecto del bootstrap de empresa.
///
/// Reglas de un step (idénticas en espíritu a <c>ICompanyBootstrapStep</c>):
///   - Solo es responsable de su propio mecanismo (nunca duplica lo que ya cubre otro step).
///   - Es idempotente: seguro de ejecutar en cada arranque.
///   - Si un step debe tolerar fallos sin bloquear el arranque de la API (como hoy
///     <c>InstallDataBootstrapStep</c>), esa resiliencia vive dentro del propio step — el
///     orquestador nunca decide selectivamente qué error ignorar.
/// </summary>
public interface IGlobalBootstrapStep
{
    /// <summary>Orden de ejecución explícito, ascendente. Ver <see cref="GlobalBootstrapStepOrder"/>.</summary>
    int Order { get; }

    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
