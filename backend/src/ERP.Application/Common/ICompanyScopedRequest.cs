namespace ERP.Application.Common;

/// <summary>
/// Marca un comando/consulta MediatR que requiere contexto operativo ERP (empresa activa).
/// También se aplica por convención de namespace a módulos Sales, Inventory, Purchases, Accounting, Cash.
/// </summary>
public interface ICompanyScopedRequest
{
    /// <summary>
    /// Si se especifica, debe coincidir con <see cref="ICurrentCompany.CompanyId"/> del JWT.
    /// No tomar company_id del body sin validar contra el claim.
    /// </summary>
    Guid? ExplicitCompanyId => null;
}

/// <summary>
/// Excluye validación de empresa (p. ej. plataforma SaaS, bootstrap, super-admin).
/// </summary>
public interface ISubscriberOnlyRequest;
