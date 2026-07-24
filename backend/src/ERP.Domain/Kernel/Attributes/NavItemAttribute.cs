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
}
