using ERP.Application.Codes;
using ERP.Domain.Modules.Ride.ValueObjects;
using ERP.Infrastructure.Ride.Qr;
using FluentAssertions;
using Moq;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// Prueba que Ride no contiene ninguna lógica de codificación de QR: delega íntegramente en
/// <see cref="IQrCodeGenerator"/> (Building Block Codes), únicamente traduciendo la clave de
/// acceso a la solicitud genérica.
/// </summary>
public sealed class RideQrCodeGeneratorTests
{
    [Fact]
    public void Generate_delegates_the_access_key_value_as_content_and_returns_the_generic_result_unchanged()
    {
        var accessKey = RideAccessKey.Create(new string('1', RideAccessKey.Length));
        var expectedBytes = new byte[] { 1, 2, 3 };

        var qrCodeGeneratorMock = new Mock<IQrCodeGenerator>();
        qrCodeGeneratorMock
            .Setup(g => g.Generate(It.Is<QrGenerationRequest>(r => r.Content == accessKey.Value)))
            .Returns(new QrGenerationResult(expectedBytes));

        var sut = new RideQrCodeGenerator(qrCodeGeneratorMock.Object);

        var result = sut.Generate(accessKey);

        result.Should().BeSameAs(expectedBytes);
    }
}
