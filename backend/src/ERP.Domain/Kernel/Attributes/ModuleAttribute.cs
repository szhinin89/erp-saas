namespace ERP.Domain.Kernel.Attributes;

/// <summary>
/// Marca una clase estática como módulo del Platform Kernel. El código se usa como
/// <c>ui_nav_groups.code</c> / <c>module_key</c>. <see cref="GroupId"/> permite fijar
/// un GUID heredado (p. ej. el grupo <c>settings</c>); si se omite, <see cref="Kernel.KernelRegistry"/>
/// deriva uno determinista a partir de <see cref="Code"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ModuleAttribute(string code) : Attribute
{
    public string Code { get; } = code;
    public string Icon { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public string? GroupId { get; init; }
}
