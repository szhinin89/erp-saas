using ERP.Application.Modules.Ride.Barcodes;
using ERP.Infrastructure.Codes.Barcodes;
using ERP.Infrastructure.Ride.Barcodes;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// Fábrica de test para el <see cref="IRideBarcodeGenerator"/> real — mismo patrón que
/// <see cref="RideQrCodeGeneratorTestFactory"/>.
/// </summary>
internal static class RideBarcodeGeneratorTestFactory
{
    public static IRideBarcodeGenerator Create() =>
        new RideBarcodeGenerator(new Code128BarcodeGenerator(NullLogger<Code128BarcodeGenerator>.Instance));
}
