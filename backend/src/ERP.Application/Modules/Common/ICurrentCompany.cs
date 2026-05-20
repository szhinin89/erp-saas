namespace ERP.Application.Common;

/// <summary>
/// Contexto operativo ERP: empresa fiscal (RUC) activa dentro del suscriptor SaaS.
/// No reutilizar <see cref="ICurrentSubscriber"/> para datos de ventas, inventario o contabilidad.
/// </summary>
public interface ICurrentCompany
{
    /// <summary>Empresa activa del request. <see cref="Guid.Empty"/> si no hay claim o no autenticado.</summary>
    Guid CompanyId { get; }

    bool IsAuthenticated { get; }

    /// <summary>True cuando <see cref="CompanyId"/> está presente en el JWT.</summary>
    bool HasCompanyContext { get; }
}
