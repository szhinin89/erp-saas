using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("admin", Icon = "🛡️", SortOrder = 60)]
public static class AdminModule
{
    [NavItem(
        "Users",
        Permission = AccessPermissions.MembershipsView,
        LabelKey = "app.nav.item.admin.users",
        SortOrder = 10,
        Id = "a1000000-0000-4000-9000-000000000008"
    )]
    public const string Users = "/access/users";

    [NavItem(
        "Roles",
        Permission = AccessPermissions.ProfilesView,
        LabelKey = "app.nav.item.admin.roles",
        SortOrder = 20,
        Id = "a1000000-0000-4000-9000-000000000007"
    )]
    public const string Roles = "/admin/roles";

    [NavItem(
        "Administration Delegation",
        Permission = AdminPermissions.DelegationView,
        LabelKey = "app.nav.item.admin.security",
        SortOrder = 30,
        Id = "a1000000-0000-4000-9000-00000000000a"
    )]
    public const string Security = "/admin/security";

    [NavItem(
        "Activity",
        Permission = AdminPermissions.ActivityView,
        LabelKey = "app.nav.item.admin.activity",
        SortOrder = 40,
        Id = "a1000000-0000-4000-9000-000000000009"
    )]
    public const string Activity = "/admin/activity";

    [NavItem(
        "Sesiones de usuario",
        Permission = AccessPermissions.SessionsView,
        LabelKey = "app.nav.item.admin.accessSessions",
        SortOrder = 45,
        Id = "a1000000-0000-4000-9000-00000000000b"
    )]
    public const string AccessSessions = "/admin/access/sessions";

    [NavItem(
        "Companies",
        Permission = SettingsPermissions.CompaniesView,
        LabelKey = "app.nav.item.erp.companies",
        SortOrder = 50,
        Id = "00000000-0000-4000-8000-000000000104"
    )]
    public const string Companies = "/companies";
}
