using ERP.Application.Codes;
using ERP.Application.Modules.Ride.Qr;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Infrastructure.Ride.Qr;

/// <summary>
/// Adaptador de <see cref="IRideQrCodeGenerator"/> sobre el Building Block transversal
/// <see cref="IQrCodeGenerator"/> (ERP.Application.Codes). Ride no contiene ninguna lógica de
/// generación de QR: únicamente traduce la clave de acceso ya presente en el XML autorizado
/// (ADR-025 §10/§17) al contrato genérico y devuelve el PNG resultante.
/// </summary>
public sealed class RideQrCodeGenerator : IRideQrCodeGenerator
{
    private readonly IQrCodeGenerator _qrCodeGenerator;

    public RideQrCodeGenerator(IQrCodeGenerator qrCodeGenerator)
    {
        _qrCodeGenerator = qrCodeGenerator;
    }

    public byte[] Generate(RideAccessKey accessKey)
    {
        var request = new QrGenerationRequest(accessKey.Value);
        return _qrCodeGenerator.Generate(request).PngBytes;
    }
}
