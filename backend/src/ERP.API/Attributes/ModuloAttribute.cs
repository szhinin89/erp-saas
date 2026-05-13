namespace ERP.API.Attributes;

/// <summary>
/// Describe un módulo o subítem de menú asociado a un controlador o acción HTTP.
/// <see cref="ERP.API.Services.ModuloDiscoveryService"/> sincroniza estos metadatos con la tabla <c>funcionalidades</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class ModuloAttribute : Attribute
{
    public string Nombre { get; }
    public string? Icono { get; }
    public string? Ruta { get; }
    /// <summary>Clave única (p. ej. <c>perm:ventas.view</c>). No usar prefijo <c>SuperAdmin</c> si debe excluirse del catálogo tenant.</summary>
    public string PermisoBase { get; }
    /// <summary>Permiso del padre en catálogo (mismo valor que <see cref="PermisoBase"/> del padre).</summary>
    public string? Padre { get; }
    public int Orden { get; }
    public bool VisibleEnMenu { get; set; } = true;
    public bool EsSuperAdmin { get; set; }

    public ModuloAttribute(string nombre, string permisoBase, string? icono = null, string? ruta = null, string? padre = null, int orden = 0)
    {
        Nombre = nombre;
        PermisoBase = permisoBase;
        Icono = icono;
        Ruta = ruta;
        Padre = padre;
        Orden = orden;
    }
}
