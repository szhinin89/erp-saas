namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Permisos del módulo Employees (ADR-050 §6). Separados de MasterData: <c>employees.create</c>
/// no autoriza operaciones de BusinessPartner. Declarados sin <c>EmployeesModule</c>: hasta EMP-E7
/// solo los recibe Admin y ningún perfil existente los hereda.
/// </summary>
public static class EmployeesPermissions
{
    public const string View = "employees.view";
    public const string Create = "employees.create";
    public const string Update = "employees.update";

    /// <summary>Baja y reincorporación (P-03).</summary>
    public const string Disable = "employees.disable";
    public const string AssignmentsManage = "employees.assignments.manage";
    public const string LinkUser = "employees.link-user";
    public const string CatalogsView = "employees.catalogs.view";
    public const string CatalogsManage = "employees.catalogs.manage";
}
