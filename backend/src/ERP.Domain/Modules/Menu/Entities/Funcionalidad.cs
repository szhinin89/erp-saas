namespace ERP.Domain.Modules.Menu.Entities;

/// <summary>Catálogo de módulos / pantallas descubiertos desde la API (reflexión + <c>ModuloAttribute</c>).</summary>
public sealed class Funcionalidad
{
    public const int NombreMaxLen = 200;
    public const int IconoMaxLen = 64;
    public const int RutaMaxLen = 512;
    public const int PermisoMaxLen = 256;

    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = null!;
    public string? Icono { get; private set; }
    public string? Ruta { get; private set; }
    /// <summary>Clave estable (p. ej. <c>perm:ventas.view</c>) — única en catálogo.</summary>
    public string Permiso { get; private set; } = null!;
    public Guid? PadreId { get; private set; }
    public int Orden { get; private set; }
    public bool VisibleEnMenu { get; private set; }
    public bool EsSuperAdmin { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Funcionalidad() { }

    public static Funcionalidad Create(
        string nombre,
        string? icono,
        string? ruta,
        string permiso,
        Guid? padreId,
        int orden,
        bool visibleEnMenu,
        bool esSuperAdmin,
        DateTime utcNow)
    {
        var p = (permiso ?? string.Empty).Trim();
        if (p.Length == 0 || p.Length > PermisoMaxLen)
            throw new ArgumentException("Permiso inválido.", nameof(permiso));

        return new Funcionalidad
        {
            Id = Guid.NewGuid(),
            Nombre = (nombre ?? string.Empty).Trim(),
            Icono = string.IsNullOrWhiteSpace(icono) ? null : icono.Trim(),
            Ruta = string.IsNullOrWhiteSpace(ruta) ? null : ruta.Trim(),
            Permiso = p,
            PadreId = padreId,
            Orden = orden,
            VisibleEnMenu = visibleEnMenu,
            EsSuperAdmin = esSuperAdmin,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    public void SyncFromDiscovery(
        string nombre,
        string? icono,
        string? ruta,
        Guid? padreId,
        int orden,
        bool visibleEnMenu,
        bool esSuperAdmin,
        DateTime utcNow)
    {
        Nombre = (nombre ?? string.Empty).Trim();
        Icono = string.IsNullOrWhiteSpace(icono) ? null : icono.Trim();
        Ruta = string.IsNullOrWhiteSpace(ruta) ? null : ruta.Trim();
        PadreId = padreId;
        Orden = orden;
        VisibleEnMenu = visibleEnMenu;
        EsSuperAdmin = esSuperAdmin;
        UpdatedAtUtc = utcNow;
    }
}
