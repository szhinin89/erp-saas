using ERP.Application.Navigation.DTOs;

namespace ERP.API.Contracts;

public sealed record ReorderNavigationGroupsRequest(IReadOnlyList<Guid> OrderedGroupIds);

public sealed record ReorderNavigationItemLevelsRequest(
    IReadOnlyList<NavItemSiblingOrderDto> Levels
);
