namespace ERP.API.Attributes;

/// <summary>
/// Describes a menu module or sub-item associated with a controller or HTTP action.
/// <see cref="ERP.API.Services.AppFeatureDiscoveryService"/> syncs these metadata with the <c>app_features</c> table.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class AppFeatureAttribute : Attribute
{
    public string Name { get; }
    public string? Icon { get; }
    public string? Path { get; }
    /// <summary>Unique key (e.g. <c>perm:ventas.view</c>). Do not use <c>operador platform</c> prefix if it must be excluded from the tenant catalog.</summary>
    public string Permission { get; }
    /// <summary>Parent permission in the catalog (same value as <see cref="Permission"/> of the parent).</summary>
    public string? ParentPermission { get; }
    public int SortOrder { get; }
    public bool IsVisibleInMenu { get; set; } = true;
    public bool IsPlatformOnlyFeature { get; set; }

    public AppFeatureAttribute(string name, string permission, string? icon = null, string? path = null, string? parentPermission = null, int sortOrder = 0)
    {
        Name = name;
        Permission = permission;
        Icon = icon;
        Path = path;
        ParentPermission = parentPermission;
        SortOrder = sortOrder;
    }
}
