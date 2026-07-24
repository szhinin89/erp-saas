namespace ERP.Application.Common;

/// <summary>
/// Marca un comando/consulta MediatR que requiere contexto operativo ERP (empresa activa).
/// Fuente primaria de <c>CompanyScopeBehavior</c> (no depender del namespace).
/// </summary>
public interface ICompanyScopedRequest
{
    /// <summary>
    /// Si se especifica, debe coincidir con el company_id del contexto activo (X-Company-Id header).
    /// No tomar company_id del body sin validar contra el contexto.
    /// </summary>
    Guid? ExplicitCompanyId => null;
}

/// <summary>
/// Marca requests tenant-scoped sin empresa operativa activa.
/// Ejemplo: BusinessPartner global al tenant (no requiere empresa seleccionada).
/// </summary>
public interface ITenantScopedRequest;

/// <summary>
/// Alias legacy — preferir <see cref="ITenantScopedRequest"/>.
/// </summary>
public interface ITenantOnlyRequest : ITenantScopedRequest;

/// <summary>
/// Marca requests de sistema / cross-tenant / jobs de background sin contexto ERP operativo.
/// </summary>
public interface IPlatformScopedRequest;

/// <summary>
/// Equivalente a <see cref="ICompanyScopedRequest"/> con company context implícito.
/// </summary>
public interface IRequiresCompanyContext : ICompanyScopedRequest;

/// <summary>
/// Marca un comando/consulta MediatR que requiere contexto operativo de sucursal
/// (<c>ICurrentBranch</c> + <c>CompanyUserBranch</c> activa, validado por
/// <c>IBranchAccessGuard</c>/<c>BranchScopeBehavior</c>). Hereda de
/// <see cref="ICompanyScopedRequest"/> — una sucursal nunca existe sin empresa operativa, por
/// lo que toda request branch-scoped es también company-scoped; así <c>CompanyScopeBehavior</c>
/// sigue validando el nivel de empresa sin que cada módulo tenga que declarar ambos marcadores
/// por separado (orden de pipeline: Tenant → Company → Branch).
/// </summary>
public interface IBranchScopedRequest : ICompanyScopedRequest;

/// <summary>
/// Marca un comando/consulta MediatR cuya operación involucra dos sucursales potencialmente
/// distintas (origen y destino), como una transferencia de inventario entre bodegas. A
/// diferencia de <see cref="IBranchScopedRequest"/>, esta interfaz es puramente semántica: no
/// tiene ningún <c>IPipelineBehavior</c> asociado (no existe un "InterBranchScopeBehavior").
/// La resolución de ambas sucursales (vía <c>Warehouse.BranchId</c>) y su autorización se
/// validan explícitamente dentro del handler correspondiente, típicamente a través de
/// <c>IInterBranchAccessGuard</c>. Hereda de <see cref="ICompanyScopedRequest"/> — la empresa
/// operativa activa sigue siendo obligatoria y se valida igual que cualquier otro request.
/// </summary>
public interface IInterBranchOperationRequest : ICompanyScopedRequest;
