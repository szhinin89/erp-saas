namespace ERP.Application.Common;

/// <summary>
/// Marker interface: el comando/query requiere company_id válido en el JWT.
/// Alternativa explícita al namespace-prefix matching de CompanyScopeBehavior.
///
/// Usar en handlers nuevos cuyo namespace no esté registrado en
/// CompanyScopeBehavior.CompanyScopedNamespacePrefixes.
/// No usar en comandos que ya residen en namespaces cubiertos por el prefix-list.
///
/// Diferencia con ICompanyScopedRequest: este marker cubre el caso implícito
/// (company_id viene del JWT); ICompanyScopedRequest cubre el caso explícito
/// (el comando lleva un ExplicitCompanyId propio).
/// </summary>
public interface IRequiresCompanyContext { }
