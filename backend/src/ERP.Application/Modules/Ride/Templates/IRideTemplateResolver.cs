namespace ERP.Application.Modules.Ride.Templates;

/// <summary>
/// Resuelve <see cref="IRideTemplate"/> a partir de un <see cref="RideTemplateSelector"/>.
/// Hoy resuelve únicamente por tipo de comprobante; la jerarquía completa (ADR-025 §11) cambia
/// la implementación de esta interfaz, nunca su firma ni a sus llamadores.
/// </summary>
public interface IRideTemplateResolver
{
    IRideTemplate? Resolve(RideTemplateSelector selector);
}
