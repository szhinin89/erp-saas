using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("caja", Icon = "💵", SortOrder = 45)]
public static class CajaModule
{
    // Permission alineado con el GET/listado real de CashRegisterController (perm:caja.view):
    // create/update/enable/disable siguen protegidos por CajaPermissions.Manage a nivel de API.
    [NavItem(
        "Administración de Cajas",
        Permission = CajaPermissions.View,
        LabelKey = "app.nav.item.caja.registers",
        SortOrder = 5,
        Id = "f5000000-0000-4000-9000-000000000002"
    )]
    public const string Registers = "/cash/registers";

    [NavItem(
        "Sesiones de Caja",
        Permission = CajaPermissions.View,
        LabelKey = "app.nav.item.caja.sessions",
        SortOrder = 10,
        Id = "f5000000-0000-4000-9000-000000000001"
    )]
    public const string Sessions = "/cash";
}
