using ERP.Infrastructure.Seeding.Global.Steps;
using ERP.Infrastructure.Seeding.InstallData;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// Único step con lógica propia (no pura delegación): debe preservar exactamente el
/// comportamiento no bloqueante que antes vivía como try/catch en Program.cs — un fallo de
/// InstallData nunca debe impedir el arranque de la API.
/// </summary>
public sealed class InstallDataBootstrapStepTests
{
    [Fact]
    public async Task Si_InstallData_falla_el_step_no_relanza_la_excepcion()
    {
        var installData = new Mock<IInstallDataBootstrapService>();
        installData.Setup(s => s.ApplyPendingAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var step = new InstallDataBootstrapStep(installData.Object, NullLogger<InstallDataBootstrapStep>.Instance);

        var act = async () => await step.ExecuteAsync(CancellationToken.None);

        await act.Should().NotThrowAsync(
            "un fallo de InstallData no debe bloquear el arranque de la API, igual que antes de este refactor");
    }

    [Fact]
    public async Task Delega_en_IInstallDataBootstrapService()
    {
        var installData = new Mock<IInstallDataBootstrapService>();
        var step = new InstallDataBootstrapStep(installData.Object, NullLogger<InstallDataBootstrapStep>.Instance);

        await step.ExecuteAsync(CancellationToken.None);

        installData.Verify(s => s.ApplyPendingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
