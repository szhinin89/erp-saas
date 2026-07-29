namespace ERP.Domain.Modules.Menu.Interfaces;

public sealed record AppFeatureMenuRow(
    Guid Id,
    string Name,
    string? Icon,
    string? Path,
    string Permission,
    Guid? ParentId,
    int SortOrder
);

public sealed record AppFeatureSyncRow(
    string Permission,
    string Name,
    string? Icon,
    string? Path,
    string? ParentPermission,
    int SortOrder,
    bool IsVisibleInMenu
);

public interface IAppFeatureRepository
{
    Task<IReadOnlyList<AppFeatureMenuRow>> ListVisibleMenuRowsAsync(
        CancellationToken cancellationToken = default
    );

    Task<int> SyncDiscoveredFeaturesAsync(
        IReadOnlyList<AppFeatureSyncRow> rows,
        CancellationToken cancellationToken = default
    );
}
