using ERP.Infrastructure.Seeding.Global;
using ERP.Infrastructure.Seeding.Global.Steps;
using ERP.Infrastructure.Seeding.InstallData;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// La carga obligatoria INEC debe completarse antes de permitir el arranque de la API.
/// </summary>
public sealed class InstallDataBootstrapStepTests
{
    [Fact]
    public async Task Si_InstallData_falla_el_step_bloquea_el_arranque()
    {
        var installData = new Mock<IInstallDataBootstrapService>();
        installData
            .Setup(s => s.ApplyPendingAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var step = new InstallDataBootstrapStep(
            installData.Object,
            NullLogger<InstallDataBootstrapStep>.Instance
        );

        var act = async () => await step.ExecuteAsync(CancellationToken.None);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("boom");
    }

    [Fact]
    public async Task Si_INEC_falla_el_bootstrap_global_se_detiene()
    {
        var failure = new InvalidOperationException("INEC failed");
        var installData = new Mock<IInstallDataBootstrapService>();
        installData.Setup(s => s.ApplyPendingAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var nextStep = new Mock<IGlobalBootstrapStep>();
        nextStep.SetupGet(s => s.Order).Returns(GlobalBootstrapStepOrder.InstallData + 1);
        var orchestrator = new GlobalBootstrapOrchestrator(
            new IGlobalBootstrapStep[]
            {
                new InstallDataBootstrapStep(installData.Object, NullLogger<InstallDataBootstrapStep>.Instance),
                nextStep.Object,
            },
            NullLogger<GlobalBootstrapOrchestrator>.Instance
        );

        var act = async () => await orchestrator.RunAsync();

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
        nextStep.Verify(s => s.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delega_en_IInstallDataBootstrapService()
    {
        var installData = new Mock<IInstallDataBootstrapService>();
        var step = new InstallDataBootstrapStep(
            installData.Object,
            NullLogger<InstallDataBootstrapStep>.Instance
        );

        await step.ExecuteAsync(CancellationToken.None);

        installData.Verify(s => s.ApplyPendingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
