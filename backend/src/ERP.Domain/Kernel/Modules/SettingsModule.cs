using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Permissions;

namespace ERP.Domain.Kernel.Modules;

[Module("settings", Icon = "⚙", SortOrder = 50, GroupId = "f2d0ca10-0000-4000-8000-000000000008")]
public static class SettingsModule
{
    [NavItem("Company", Permission = SettingsPermissions.CompanyView,
        LabelKey = "app.nav.item.settings.company", SortOrder = 10,
        Id = "00000000-0000-4000-8000-000000000101")]
    public const string Company = "/settings/company";

    [NavItem("Branches", Permission = SettingsPermissions.BranchesView,
        LabelKey = "app.nav.item.settings.branches", SortOrder = 20,
        Id = "a1000000-0000-4000-9000-000000000005")]
    public const string Branches = "/settings/branches";

    [NavItem("Establishments", Permission = SettingsPermissions.EstablishmentsView,
        LabelKey = "app.nav.item.settings.establishments", SortOrder = 30,
        Id = "a1000000-0000-4000-9000-000000000010")]
    public const string Establishments = "/settings/establishments";

    [NavItem("Emission Points", Permission = SettingsPermissions.EmissionPointsView,
        LabelKey = "app.nav.item.settings.emissionPoints", SortOrder = 40,
        Id = "a1000000-0000-4000-9000-00000000000f")]
    public const string EmissionPoints = "/settings/emission-points";

    [NavItem("Geography", Permission = SettingsPermissions.GeographyView,
        LabelKey = "app.nav.item.settings.geography", SortOrder = 50,
        Id = "a1000000-0000-4000-9000-000000000006")]
    public const string Geography = "/settings/geography";
}
