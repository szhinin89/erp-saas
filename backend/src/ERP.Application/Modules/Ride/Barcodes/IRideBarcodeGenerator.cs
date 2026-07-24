using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Barcodes;

/// <summary>
/// Genera el código de barras Code128 del RIDE exclusivamente a partir de la clave de acceso ya
/// presente en el XML autorizado — nunca consulta entidades de negocio ni recalcula nada. Mismo
/// patrón que <c>IRideQrCodeGenerator</c> (contrato interno de Ride, ADR-025 §8): Ride depende
/// únicamente de esta abstracción, nunca del Building Block Codes directamente.
/// </summary>
public interface IRideBarcodeGenerator
{
    byte[] Generate(RideAccessKey accessKey);
}
