namespace ERP.Domain.Navigation.Entities;

/// <summary>
/// Grupo del menú lateral (definición global, no multi-tenant).
/// Las etiquetas se resuelven en el cliente con <c>t(labelKey)</c>.
/// </summary>
public sealed class UiNavGroup
{
    public Guid Id { get; private set; }
    /// <summary>Código estable (p. ej. home, catalog) usado por el front para lógica UI.</summary>
    public string Code { get; private set; } = null!;
    public string Icon { get; private set; } = null!;
    /// <summary>Clave i18n (p. ej. app.nav.group.catalog).</summary>
    public string LabelKey { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public string? ModuleKey { get; private set; }
    /// <summary>Roles permitidos separados por coma; null o vacío = todos.</summary>
    public string? RolesCsv { get; private set; }
    public bool RequireSuperAdminPanel { get; private set; }
    public bool IsActive { get; private set; }

    private UiNavGroup() { }

    public static UiNavGroup Create(
        Guid id,
        string code,
        string icon,
        string labelKey,
        int sortOrder,
        string? moduleKey,
        string? rolesCsv,
        bool requireSuperAdminPanel,
        bool isActive = true)
    {
        return new UiNavGroup
        {
            Id = id,
            Code = (code ?? string.Empty).Trim().ToLowerInvariant(),
            Icon = icon ?? string.Empty,
            LabelKey = (labelKey ?? string.Empty).Trim(),
            SortOrder = sortOrder,
            ModuleKey = string.IsNullOrWhiteSpace(moduleKey) ? null : moduleKey.Trim().ToLowerInvariant(),
            RolesCsv = string.IsNullOrWhiteSpace(rolesCsv) ? null : rolesCsv.Trim(),
            RequireSuperAdminPanel = requireSuperAdminPanel,
            IsActive = isActive,
        };
    }
}
