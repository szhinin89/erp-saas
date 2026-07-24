namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Bundle de permisos para el perfil "DataEntry" (operador de datos maestros).
/// Consumido por <c>DefaultProfileSeeder</c>. No es un permiso en sí mismo — sus
/// claves ya están incluidas en <see cref="KernelRegistry.Permissions"/> a través
/// de los catálogos de cada módulo.
/// </summary>
public static class DataEntryProfileBundle
{
    public static IReadOnlyList<string> Permissions { get; } =
    [
        MasterDataPermissions.BusinessPartnersView,
        MasterDataPermissions.BusinessPartnersCreate,
        MasterDataPermissions.BusinessPartnersUpdate,
        MasterDataPermissions.BusinessPartnersDisable,
        InventoryPermissions.ItemsView,
        InventoryPermissions.ItemsCreate,
        InventoryPermissions.ItemsEdit,
        SettingsPermissions.BranchesView,
        InventoryPermissions.WarehousesView,
    ];
}
