using ERP.Application.Common;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.GetSystemProviderSettings;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.UpsertSystemProviderSettings;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.ElectronicInvoicing;

/// <summary>ERP-CORE-CLOSEOUT-09 — Get/Upsert del singleton de proveedor de sistema (no company-scoped).</summary>
public sealed class SystemProviderSettingsHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ISystemProviderSettingsRepository> Repo { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Fixture() => User.Setup(u => u.UserId).Returns(UserId);

        public GetSystemProviderSettingsQueryHandler BuildGetHandler() => new(Repo.Object);

        public UpsertSystemProviderSettingsCommandHandler BuildUpsertHandler() =>
            new(Repo.Object, User.Object);
    }

    [Fact]
    public async Task Get_sin_registro_devuelve_valores_por_defecto_no_configurado()
    {
        var f = new Fixture();
        f.Repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((SystemProviderSettings?)null);

        var result = await f.BuildGetHandler().Handle(new GetSystemProviderSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.IsFullyConfigured.Should().BeFalse();
        result.Value.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task Upsert_primera_vez_crea_el_singleton()
    {
        var f = new Fixture();
        f.Repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((SystemProviderSettings?)null);

        var result = await f.BuildUpsertHandler()
            .Handle(
                new UpsertSystemProviderSettingsCommand(
                    "1790012345001",
                    "ZH Technologies S.A.",
                    "J62021002",
                    new DateOnly(2026, 8, 21),
                    Enabled: true
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Enabled.Should().BeTrue();
        result.Value.IsFullyConfigured.Should().BeTrue();
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SystemProviderSettings>(), It.IsAny<CancellationToken>()), Times.Once);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upsert_habilitar_sin_datos_completos_devuelve_ValidationFailure()
    {
        var f = new Fixture();
        f.Repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((SystemProviderSettings?)null);

        var result = await f.BuildUpsertHandler()
            .Handle(
                new UpsertSystemProviderSettingsCommand(null, null, null, null, Enabled: true),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeFalse();
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SystemProviderSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upsert_sobre_registro_existente_actualiza_sin_AddAsync()
    {
        var existing = SystemProviderSettings.CreateNew();
        var f = new Fixture();
        f.Repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await f.BuildUpsertHandler()
            .Handle(
                new UpsertSystemProviderSettingsCommand(
                    "1790012345001",
                    "ZH Technologies S.A.",
                    "J62021002",
                    null,
                    Enabled: false
                ),
                CancellationToken.None
            );

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Repo.Verify(r => r.AddAsync(It.IsAny<SystemProviderSettings>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
