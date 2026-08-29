namespace ERP.Domain.Kernel.Attributes;

/// <summary>
/// Marca una constante <c>const string</c> (ruta del frontend) dentro de una clase
/// <see cref="ModuleAttribute"/> como ítem de navegación global. <see cref="Permission"/>
/// debe referenciar una constante de <c>ERP.Domain.Kernel.Permissions</c> (o ser <c>null</c>
/// si el ítem no requiere permiso). <see cref="Id"/> permite fijar un GUID heredado; si se
/// omite, <see cref="Kernel.KernelRegistry"/> deriva uno determinista a partir de la ruta.
/// <see cref="ParentId"/> agrupa el ítem visualmente bajo otro ítem (contenedor) del mismo
/// módulo, referenciando su <c>Id</c>. <see cref="PermissionsAnyCsv"/> es una lista CSV de
/// claves de permiso (OR) usada típicamente por ítems contenedor para mostrarse solo si el
/// usuario puede ver al menos uno de sus hijos.
/// <see cref="RelatedActionPermissionsCsv"/> (ADMIN-PERMISSIONS-SSOT-KERNEL-02) es una lista CSV
/// de constantes de permiso reales del mismo dominio funcional que este NavItem (crear/
/// actualizar/eliminar/confirmar/reversar/etc., construida concatenando constantes — no strings
/// sueltos, para seguridad en tiempo de compilación), expuestas como acciones asignables en la
/// pantalla Asignación de permisos junto al permiso de acceso (<see cref="Permission"/>) de este
/// NavItem. No afecta visibilidad de menú — solo alimenta
/// <see cref="Kernel.KernelRegistry.AssignablePermissionKeys"/>. Se deja vacío cuando la pantalla
/// no tiene acciones granulares reales distintas de su permiso de acceso — no se inventa
/// granularidad que la API no exige.
/// <see cref="FeatureKey"/>/<see cref="RequiresExternalEntitlement"/> (SECURITY-PERMISSION-SCOPE-01)
/// son metadata opcional puramente declarativa para una futura plataforma SaaS externa conectada
/// por API — hoy no gatean nada (el consumidor de esta metadata,
/// <c>IExternalEntitlementService</c> en <c>ERP.Application</c>, es NoOp/permisivo). Se deja
/// vacío/false salvo que la pantalla ya tenga un feature de plan identificado.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class NavItemAttribute(string label) : Attribute
{
    public string Label { get; } = label;
    public string? Permission { get; init; }
    public string LabelKey { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public string? Id { get; init; }
    public string? ParentId { get; init; }
    public string? PermissionsAnyCsv { get; init; }
    public string? RelatedActionPermissionsCsv { get; init; }
    public string? FeatureKey { get; init; }
    public bool RequiresExternalEntitlement { get; init; }
}
