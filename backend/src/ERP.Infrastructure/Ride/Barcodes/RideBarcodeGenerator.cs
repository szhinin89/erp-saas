using ERP.Application.Codes.Barcodes;
using ERP.Application.Modules.Ride.Barcodes;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Infrastructure.Ride.Barcodes;

/// <summary>
/// Adaptador de <see cref="IRideBarcodeGenerator"/> sobre el Building Block transversal
/// <see cref="IBarcodeGenerator"/> (ERP.Application.Codes.Barcodes). Ride no contiene ninguna
/// lógica de generación de código de barras: únicamente traduce la clave de acceso ya presente
/// en el XML autorizado al contrato genérico y devuelve el PNG resultante — mismo patrón que
/// <c>RideQrCodeGenerator</c>.
/// </summary>
public sealed class RideBarcodeGenerator : IRideBarcodeGenerator
{
    private readonly IBarcodeGenerator _barcodeGenerator;

    public RideBarcodeGenerator(IBarcodeGenerator barcodeGenerator)
    {
        _barcodeGenerator = barcodeGenerator;
    }

    public byte[] Generate(RideAccessKey accessKey)
    {
        var request = new BarcodeGenerationRequest(accessKey.Value, BarcodeSymbology.Code128, width: 600, height: 100);
        return _barcodeGenerator.Generate(request).PngBytes;
    }
}
