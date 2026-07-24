namespace ERP.Application.Navigation.DTOs;

/// <summary>Grupo del menú lateral ya filtrado por permisos del backend — seguro para consumo directo en frontend.</summary>
public sealed record NavMenuGroupDto(
    Guid   Id,
    string Code,
    string Icon,
    string LabelKey,
    IReadOnlyList<NavMenuItemDto> Items);

/// <summary>Ítem de menú ya filtrado — sin metadatos de permiso ni rol.</summary>
public sealed record NavMenuItemDto(
    Guid   Id,
    string LabelKey,
    string? DisplayLabel,
    string RoutePath,
    IReadOnlyList<NavMenuItemDto> Children);
