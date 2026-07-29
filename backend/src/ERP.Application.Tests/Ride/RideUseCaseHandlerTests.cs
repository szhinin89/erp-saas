using ERP.Application.Common;
using ERP.Application.Modules.Ride.DTOs;
using ERP.Application.Modules.Ride.Services;
using ERP.Application.Modules.Ride.UseCases.GetOrGenerateRide;
using ERP.Application.Modules.Ride.UseCases.RegenerateRide;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Ride;

/// <summary>
/// Fase 5 (ADR-025): ambos handlers delegan exclusivamente en <see cref="IRideDocumentService"/>,
/// resolviendo tenant/company desde el contexto ambiente — sin lógica propia. Reemplaza a la
/// versión de Fase 3 (que verificaba <see cref="NotImplementedException"/>, ya no aplicable
/// porque la orquestación real ya existe).
/// </summary>
public sealed class RideUseCaseHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private const string SourceModule = "Sales";
    private static readonly Guid SourceEntityId = Guid.NewGuid();

    private static readonly RideGenerationResultDto ExpectedResult = new(
        RideOutcome.Generated,
        "ride/path.pdf",
        null,
        null
    );

    [Fact]
    public async Task GetOrGenerateRideQueryHandler_delegates_only_to_IRideDocumentService_GetOrGenerateAsync()
    {
        var service = new Mock<IRideDocumentService>();
        service
            .Setup(s =>
                s.GetOrGenerateAsync(
                    TenantId,
                    CompanyId,
                    SourceModule,
                    SourceEntityId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<RideGenerationResultDto>.Success(ExpectedResult));
        var currentTenant = new FixedCurrentTenant(TenantId);
        var currentCompany = new FixedCurrentCompany(CompanyId);
        var handler = new GetOrGenerateRideQueryHandler(
            service.Object,
            currentTenant,
            currentCompany
        );

        var result = await handler.Handle(
            new GetOrGenerateRideQuery(SourceModule, SourceEntityId),
            CancellationToken.None
        );

        result.Value.Should().Be(ExpectedResult);
        service.Verify(
            s =>
                s.GetOrGenerateAsync(
                    TenantId,
                    CompanyId,
                    SourceModule,
                    SourceEntityId,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        service.Verify(
            s =>
                s.RegenerateAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RegenerateRideCommandHandler_delegates_only_to_IRideDocumentService_RegenerateAsync()
    {
        var service = new Mock<IRideDocumentService>();
        service
            .Setup(s =>
                s.RegenerateAsync(
                    TenantId,
                    CompanyId,
                    SourceModule,
                    SourceEntityId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<RideGenerationResultDto>.Success(ExpectedResult));
        var currentTenant = new FixedCurrentTenant(TenantId);
        var currentCompany = new FixedCurrentCompany(CompanyId);
        var handler = new RegenerateRideCommandHandler(
            service.Object,
            currentTenant,
            currentCompany
        );

        var result = await handler.Handle(
            new RegenerateRideCommand(SourceModule, SourceEntityId),
            CancellationToken.None
        );

        result.Value.Should().Be(ExpectedResult);
        service.Verify(
            s =>
                s.RegenerateAsync(
                    TenantId,
                    CompanyId,
                    SourceModule,
                    SourceEntityId,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        service.Verify(
            s =>
                s.GetOrGenerateAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }
}
