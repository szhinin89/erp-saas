namespace ERP.Application.Common;

/// <summary>
/// Resuelve la empresa operativa por defecto de un suscriptor (compatibilidad y login).
/// </summary>
public interface ICompanyContextResolver
{
    Task<Guid?> ResolveDefaultCompanyIdAsync(Guid subscriberId, CancellationToken ct = default);

    Task<int> CountActiveCompaniesAsync(Guid subscriberId, CancellationToken ct = default);
}
