namespace ERP.Application.Common.Config;

/// <summary>
/// Umbrales del monitor central de salud SRI (<c>SriServiceHealthMonitor</c>) — sección
/// "SriHealth" de appsettings. Son umbrales técnicos de observación, nunca reglas fiscales: no
/// deciden si un comprobante se emite ni cómo se interpreta una respuesta del SRI.
/// </summary>
public sealed class SriHealthOptions
{
    public const string Section = "SriHealth";

    /// <summary>Antigüedad máxima de una observación para contar como evidencia; sin evidencia vigente el estado es "Sin verificar".</summary>
    public int EvidenceFreshnessMinutes { get; init; } = 15;

    /// <summary>Llamadas SOAP reales consecutivas con fallo de transporte necesarias para indicar posible indisponibilidad.</summary>
    public int ConsecutiveTransportFailuresForUnavailable { get; init; } = 2;

    /// <summary>Duración de una llamada individual (respondida) a partir de la cual se considera lenta.</summary>
    public int SlowCallThresholdSeconds { get; init; } = 8;
}
