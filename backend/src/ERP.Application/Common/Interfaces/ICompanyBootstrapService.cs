namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Crea los datos operativos mínimos cuando se registra una nueva empresa (Company) en un Subscriber.
/// Llamar desde <c>CreateCompanyHandler</c> después de persistir la Company.
///
/// Pasos actuales (idempotentes — seguros si se llaman más de una vez):
///   1. Sucursal Principal  — Branch vinculada a la empresa
///   2. Bodega Principal    — Warehouse company-scoped vinculada a la sucursal
/// </summary>
public interface ICompanyBootstrapService
{
    Task BootstrapCompanyAsync(Guid subscriberId, Guid companyId, Guid actorId, CancellationToken ct = default);
}
