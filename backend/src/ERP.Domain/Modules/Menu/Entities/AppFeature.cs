namespace ERP.Domain.Modules.Menu.Entities;

public sealed class AppFeature
{
    public const int NameMaxLen       = 200;
    public const int IconMaxLen       = 64;
    public const int PathMaxLen       = 512;
    public const int PermissionMaxLen = 256;

    public Guid     Id            { get; private set; }
    public string   Name          { get; private set; } = null!;
    public string?  Icon          { get; private set; }
    public string?  Path          { get; private set; }
    public string   Permission    { get; private set; } = null!;
    public Guid?    ParentId      { get; private set; }
    public int      SortOrder     { get; private set; }
    public bool     IsVisibleInMenu { get; private set; }
    public bool     IsPlatformOnlyFeature  { get; private set; }
    public DateTime CreatedAtUtc  { get; private set; }
    public DateTime UpdatedAtUtc  { get; private set; }

    private AppFeature() { }

    public static AppFeature Create(
        string   name,
        string?  icon,
        string?  path,
        string   permission,
        Guid?    parentId,
        int      sortOrder,
        bool     isVisibleInMenu,
        bool     isSuperAdmin,
        DateTime utcNow)
    {
        var p = (permission ?? string.Empty).Trim();
        if (p.Length == 0 || p.Length > PermissionMaxLen)
            throw new ArgumentException("Invalid permission.", nameof(permission));

        return new AppFeature
        {
            Id              = Guid.NewGuid(),
            Name            = (name ?? string.Empty).Trim(),
            Icon            = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim(),
            Path            = string.IsNullOrWhiteSpace(path) ? null : path.Trim(),
            Permission      = p,
            ParentId        = parentId,
            SortOrder       = sortOrder,
            IsVisibleInMenu = isVisibleInMenu,
            IsPlatformOnlyFeature    = isSuperAdmin,
            CreatedAtUtc    = utcNow,
            UpdatedAtUtc    = utcNow,
        };
    }

    public void SyncFromDiscovery(
        string   name,
        string?  icon,
        string?  path,
        Guid?    parentId,
        int      sortOrder,
        bool     isVisibleInMenu,
        bool     isSuperAdmin,
        DateTime utcNow)
    {
        Name            = (name ?? string.Empty).Trim();
        Icon            = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
        Path            = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        ParentId        = parentId;
        SortOrder       = sortOrder;
        IsVisibleInMenu = isVisibleInMenu;
        IsPlatformOnlyFeature    = isSuperAdmin;
        UpdatedAtUtc    = utcNow;
    }
}
