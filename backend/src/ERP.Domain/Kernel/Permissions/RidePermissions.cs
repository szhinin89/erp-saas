namespace ERP.Domain.Kernel.Permissions;

/// <summary>
/// Permisos de acceso al RIDE (Representación Impresa del Documento Electrónico) desde los
/// módulos de negocio consumidores (Ventas hoy; Inventario, Activos, POS a futuro). Ride en sí
/// (ADR-025) no conoce estos permisos — se aplican únicamente en el borde HTTP (<c>RideController</c>).
/// </summary>
public static class RidePermissions
{
    public const string View = "ride.view";

    /// <summary>Forzar la regeneración de un RIDE aunque el cache siga siendo válido.</summary>
    public const string Regenerate = "ride.regenerate";
}
