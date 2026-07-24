namespace ERP.Domain.Kernel.Navigation;

/// <summary>
/// Módulo del Platform Kernel resuelto desde <see cref="Attributes.ModuleAttribute"/>.
/// Equivale a un grupo de navegación (<c>ui_nav_groups</c>).
/// </summary>
public sealed record ModuleDefinition(string Code, Guid GroupId, string Icon, int SortOrder);
