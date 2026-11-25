using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

// EMP-E7 (contrato Empleados V1 §18) — módulo propio del launcher, después de Proveedores.
[Module("employees", Icon = "🧑‍💼", SortOrder = 25)]
public static class EmployeesModule
{
    [NavItem(
        "Gestión de empleados",
        LabelKey = "app.nav.item.employees.managementGroup",
        SortOrder = 5,
        Id = "e7000000-0000-4000-9000-000000000001",
        PermissionsAnyCsv = EmployeesPermissions.View + "," + EmployeesPermissions.CatalogsView
    )]
    public const string ManagementGroup = "/employees/management-group";

    [NavItem(
        "Empleados",
        Permission = EmployeesPermissions.View,
        LabelKey = "app.nav.item.employees.employees",
        SortOrder = 5,
        Id = "e7000000-0000-4000-9000-000000000002",
        ParentId = "e7000000-0000-4000-9000-000000000001",
        RelatedActionPermissionsCsv = EmployeesPermissions.Create
            + ","
            + EmployeesPermissions.Update
            + ","
            + EmployeesPermissions.Disable
            + ","
            + EmployeesPermissions.AssignmentsManage
            + ","
            + EmployeesPermissions.LinkUser
    )]
    public const string Employees = "/employees";

    [NavItem(
        "Catálogos laborales",
        Permission = EmployeesPermissions.CatalogsView,
        LabelKey = "app.nav.item.employees.catalogs",
        SortOrder = 10,
        Id = "e7000000-0000-4000-9000-000000000003",
        ParentId = "e7000000-0000-4000-9000-000000000001",
        RelatedActionPermissionsCsv = EmployeesPermissions.CatalogsManage
    )]
    public const string Catalogs = "/employees/catalogs";
}
