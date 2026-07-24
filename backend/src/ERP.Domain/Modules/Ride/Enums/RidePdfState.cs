namespace ERP.Domain.Modules.Ride.Enums;

/// <summary>
/// Ciclo de vida de un <c>RidePdfDocument</c> para una huella (fingerprint) específica
/// (XML + plantilla + branding + renderer + especificación — ADR-025 §14).
///
/// <see cref="PendingSource"/> es el estado que resuelve H1 de ADR-025: un
/// <c>ElectronicDocument</c> puede estar Authorized sin tener todavía el XML autorizado
/// persistido — ese caso nunca se trata como <see cref="Failed"/>, es reintentable.
/// </summary>
public enum RidePdfState
{
    /// <summary>Registro creado, resultado del primer intento aún no conocido.</summary>
    Pending = 1,

    /// <summary>El XML autorizado todavía no está disponible en <c>ElectronicDocuments</c> — reintentable.</summary>
    PendingSource = 2,

    /// <summary>El intento de generación falló (parser, plantilla, render o storage).</summary>
    Failed = 3,

    /// <summary>PDF generado y almacenado — terminal para esta huella (ver <c>RidePdfDocument.MarkGenerated</c>).</summary>
    Generated = 4,
}
