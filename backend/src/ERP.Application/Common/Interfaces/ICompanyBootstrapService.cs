namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Crea los datos operativos mínimos cuando se registra una nueva empresa (Company) en un Tenant.
/// Invocado exclusivamente desde <c>ERP.Infrastructure.Services.CompanyProvisioningService</c> —
/// único punto de creación de Company en producción. Ningún handler debe llamarlo directamente.
///
/// Implementación: <c>ERP.Infrastructure.Seeding.CompanyBootstrapOrchestrator</c> — orquesta,
/// en orden explícito, los <see cref="ICompanyBootstrapStep"/> registrados en DI (uno por
/// dominio/módulo: Organización, Documentos Electrónicos, Inventario, Ventas, Accesos). Un nuevo
/// dominio con datos por defecto se agrega implementando un nuevo step — nunca modificando el
/// orquestador ni esta interfaz.
/// </summary>
public interface ICompanyBootstrapService
{
    Task BootstrapCompanyAsync(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        CancellationToken cancellationToken = default
    );
}
