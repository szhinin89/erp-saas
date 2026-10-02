using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.Retentions.Services;
using Moq;

namespace ERP.Application.Tests.TestSupport;

/// <summary>
/// ZH-RETENTION-ELECTRONIC-LIFECYCLE-01A — dobles compartidos para tests unitarios que no son sobre el
/// ciclo electrónico de la retención (ese comportamiento se prueba contra PostgreSQL real en
/// ERP.Infrastructure.Tests).
/// </summary>
public static class RetentionElectronicTestDoubles
{
    /// <summary>Anulación del origen sin comprobante electrónico (comportamiento previo a 01A).</summary>
    public static IElectronicDocumentSourceCancellation NoElectronicDocument()
    {
        var mock = new Mock<IElectronicDocumentSourceCancellation>();
        mock.Setup(m =>
                m.PrepareAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Result<ElectronicDocumentSourceCancellationOutcome>.Success(
                    ElectronicDocumentSourceCancellationOutcome.NoElectronicDocument
                )
            );
        return mock.Object;
    }

    /// <summary>Transmisión que registra las retenciones para las que se inició, sin tocar el SRI.</summary>
    public static Mock<IRetentionElectronicTransmission> Transmission()
    {
        var mock = new Mock<IRetentionElectronicTransmission>();
        mock.Setup(m =>
                m.StartAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<ElectronicDocumentDto>.Failure("no-op (test)"));
        return mock;
    }

    /// <summary>Resolver sin guards: el emisor conserva el comportamiento de ElectronicDocuments v1.0.</summary>
    public static IElectronicDocumentSourceLifecycleGuardResolver NoGuards() =>
        new ElectronicDocumentSourceLifecycleGuardResolver(
            Array.Empty<IElectronicDocumentSourceLifecycleGuard>()
        );
}
