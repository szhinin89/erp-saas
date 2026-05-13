namespace ERP.Domain.Navigation.Entities;

/// <summary>Menú lateral JSON por empresa; si existe, tiene prioridad sobre el menú del plan y el menú global <c>ui_nav_*</c>.</summary>
public sealed class TenantCustomMenu
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string MenuConfigJson { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private TenantCustomMenu() { }

    public static TenantCustomMenu Create(Guid tenantId, string menuConfigJson, DateTime utcNow)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId inválido.", nameof(tenantId));
        var json = (menuConfigJson ?? string.Empty).Trim();
        if (json.Length == 0)
            throw new ArgumentException("Menú vacío.", nameof(menuConfigJson));

        return new TenantCustomMenu
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MenuConfigJson = json,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void UpdateMenuJson(string menuConfigJson, DateTime utcNow)
    {
        var json = (menuConfigJson ?? string.Empty).Trim();
        if (json.Length == 0)
            throw new ArgumentException("Menú vacío.", nameof(menuConfigJson));
        MenuConfigJson = json;
        UpdatedAtUtc = utcNow;
    }
}
