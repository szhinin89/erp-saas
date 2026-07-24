using ERP.Application.Modules.Ride.Qr;
using ERP.Infrastructure.Codes;
using ERP.Infrastructure.Ride.Qr;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// Fábrica de test para el <see cref="IRideQrCodeGenerator"/> real — evita repetir
/// <c>new RideQrCodeGenerator(new QrCodeGenerator(...))</c> en cada test que construye
/// <c>QuestPdfRideRenderer</c> directamente.
/// </summary>
internal static class RideQrCodeGeneratorTestFactory
{
    public static IRideQrCodeGenerator Create() =>
        new RideQrCodeGenerator(new QrCodeGenerator(NullLogger<QrCodeGenerator>.Instance));
}
