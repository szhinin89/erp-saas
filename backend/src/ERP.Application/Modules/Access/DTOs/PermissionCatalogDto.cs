namespace ERP.Application.Access.DTOs;

/// <summary>
/// ADMIN-PERMISSIONS-SSOT-KERNEL-02 — catálogo de permisos asignables, derivado 100% de
/// <see cref="ERP.Domain.Kernel.KernelRegistry"/> (mismo origen que el menú server-driven, sin
/// catálogo paralelo). <c>LabelKey</c> en grupo/ítem sigue el mismo patrón que
/// <c>SessionMenuGroupDto</c>/<c>SessionMenuItemDto</c> — el frontend ya sabe traducirlo vía
/// <c>t()</c>. Las acciones (<see cref="PermissionCatalogActionDto"/>) sí llevan texto plano
/// (no hay infraestructura i18n por verbo de permiso).
/// </summary>
public sealed record PermissionCatalogDto(IReadOnlyList<PermissionCatalogGroupDto> Groups);

public sealed record PermissionCatalogGroupDto(
    string Code,
    string LabelKey,
    int SortOrder,
    IReadOnlyList<PermissionCatalogItemDto> Items
);

public sealed record PermissionCatalogItemDto(
    Guid Id,
    string LabelKey,
    string Route,
    string Permission,
    int SortOrder,
    IReadOnlyList<PermissionCatalogActionDto> Actions
);

public sealed record PermissionCatalogActionDto(
    string Code,
    string Label,
    string Description,
    int SortOrder
);
