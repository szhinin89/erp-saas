using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Qr;

/// <summary>
/// Genera el código QR del RIDE exclusivamente a partir de la clave de acceso ya presente en el
/// XML autorizado (ADR-025 §10/§17) — nunca consulta entidades de negocio ni recalcula nada.
/// </summary>
public interface IRideQrCodeGenerator
{
    byte[] Generate(RideAccessKey accessKey);
}
