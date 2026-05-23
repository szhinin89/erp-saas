namespace ERP.Application.Common;

/// <summary>
/// Marca un comando/consulta MediatR que requiere contexto operativo ERP (empresa activa en JWT).
/// Fuente primaria de <see cref="Behaviors.CompanyScopeBehavior"/> (no depender del namespace).
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
/// Marca requests subscriber-scoped (sin empresa operativa en JWT).
/// Ej.: BusinessPartner global al subscriber.
/// </summary>
public interface ISubscriberScopedRequest;

/// <summary>
/// Alias legacy — preferir <see cref="ISubscriberScopedRequest"/>.
/// </summary>
public interface ISubscriberOnlyRequest : ISubscriberScopedRequest;

/// <summary>
/// Marca requests de plataforma / cross-tenant / jobs de sistema sin contexto ERP operativo.
/// </summary>
public interface IPlatformScopedRequest;

/// <summary>
/// Puente de migración: equivalente a <see cref="ICompanyScopedRequest"/> con company_id implícito en JWT.
/// </summary>
public interface IRequiresCompanyContext : ICompanyScopedRequest;
