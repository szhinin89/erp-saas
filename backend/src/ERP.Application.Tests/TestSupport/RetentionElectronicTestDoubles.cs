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

    /// <summary>SRI-TOGGLE-02 — interruptor fijo (habilitada desde siempre por defecto): conserva el comportamiento previo.</summary>
    public static IElectronicEmissionSwitch Emission(bool enabled = true, DateTime? enabledSinceUtc = null) =>
        new FixedEmissionSwitch(
            enabled
                ? new ElectronicEmissionState(true, enabledSinceUtc ?? DateTime.MinValue)
                : ElectronicEmissionState.Disabled
        );

    public sealed class FixedEmissionSwitch(ElectronicEmissionState state) : IElectronicEmissionSwitch
    {
        public ElectronicEmissionState State { get; set; } = state;
        public List<(Guid TenantId, Guid CompanyId, bool LockForShare)> Calls { get; } = [];

        /// <summary>Instante persistido del origen (por defecto: ahora, posterior a toda habilitación fija).</summary>
        public DateTime? SourceActivityUtc { get; set; } = DateTime.UtcNow;
        public List<(Guid TenantId, Guid CompanyId)> ChangeLocks { get; } = [];

        public Task<DateTime?> GetSourceActivityUtcAsync(
            Guid tenantId,
            Guid companyId,
            string sourceModule,
            ERP.Domain.Modules.ElectronicDocuments.Enums.ElectronicDocumentType documentType,
            Guid sourceEntityId,
            CancellationToken ct = default
        ) => Task.FromResult(SourceActivityUtc);

        public Task LockForChangeAsync(Guid tenantId, Guid companyId, CancellationToken ct = default)
        {
            ChangeLocks.Add((tenantId, companyId));
            return Task.CompletedTask;
        }

        public Task<ElectronicEmissionState> GetAsync(
            Guid tenantId,
            Guid companyId,
            bool lockForShare,
            CancellationToken ct = default
        )
        {
            Calls.Add((tenantId, companyId, lockForShare));
            return Task.FromResult(State);
        }
    }

    /// <summary>Resolver sin guards: el emisor conserva el comportamiento de ElectronicDocuments v1.0.</summary>
    public static IElectronicDocumentSourceLifecycleGuardResolver NoGuards() =>
        new ElectronicDocumentSourceLifecycleGuardResolver(
            Array.Empty<IElectronicDocumentSourceLifecycleGuard>()
        );
}
