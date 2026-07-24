namespace ERP.Application.Modules.Ride.DTOs;

/// <summary>
/// Discrimina el resultado de negocio de <c>GetOrGenerateRideQuery</c>/<c>RegenerateRideCommand</c>
/// (ADR-025 §7). Todos estos valores viajan dentro de un <c>Result&lt;T&gt;.Success</c> — nunca
/// como <c>Result&lt;T&gt;.Failure</c>, que queda reservado a fallos de infraestructura reales
/// (ADR-025 §7). Contrato público congelado: cualquier cambio requiere una nueva ADR.
/// </summary>
public enum RideOutcome
{
    /// <summary>Primera generación exitosa para esta huella.</summary>
    Generated,

    /// <summary>Se reutilizó un PDF ya generado — huella sin cambios.</summary>
    Cached,

    /// <summary>El XML autorizado todavía no está disponible en ElectronicDocuments (H1) — reintentable.</summary>
    PendingSource,

    /// <summary>El documento de origen nunca tendrá RIDE (rechazado, cancelado, etc.) — no reintentable.</summary>
    NotApplicable,

    /// <summary>El intento de generación falló (parser, plantilla, render o storage).</summary>
    Failed,
}
