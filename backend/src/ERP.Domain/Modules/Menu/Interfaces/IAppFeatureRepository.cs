using ERP.Domain.Modules.Menu.Entities;

namespace ERP.Domain.Modules.Menu.Interfaces;

public sealed record AppFeatureMenuRow(
    Guid Id,
    string Name,
    string? Icon,
    string? Path,
    string Permission,
    Guid? ParentId,
    int SortOrder);

public sealed record AppFeatureSyncRow(
    string Permission,
    string Name,
    string? Icon,
    string? Path,
    string? ParentPermission,
    int SortOrder,
    bool IsVisibleInMenu,
    bool IsSuperAdmin);

public interface IAppFeatureRepository
{
    Task<IReadOnlyList<AppFeatureMenuRow>> ListVisibleMenuRowsAsync(CancellationToken ct = default);

    Task<int> SyncDiscoveredFeaturesAsync(IReadOnlyList<AppFeatureSyncRow> rows, CancellationToken ct = default);
}
