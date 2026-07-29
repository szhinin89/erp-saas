using ERP.Infrastructure.Seeding.Global;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// Mismo contrato de responsabilidad que CompanyBootstrapOrchestratorTests, a nivel global:
/// orden explícito por Order (no el de registro), ejecución de todos los steps, tolerancia a
/// ausencia de steps, e idempotencia de la orquestación misma.
/// </summary>
public sealed class GlobalBootstrapOrchestratorTests
{
    private static Mock<IGlobalBootstrapStep> MakeStep(int order, List<int> executionLog)
    {
        var step = new Mock<IGlobalBootstrapStep>();
        step.Setup(s => s.Order).Returns(order);
        step.Setup(s => s.ExecuteAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => executionLog.Add(order));
        return step;
    }

    [Fact]
    public async Task Ejecuta_los_steps_en_orden_ascendente_por_Order_sin_importar_el_orden_de_registro()
    {
        var executionLog = new List<int>();

        var stepB = MakeStep(20, executionLog);
        var stepA = MakeStep(10, executionLog);

        var orchestrator = new GlobalBootstrapOrchestrator(
            new[] { stepB.Object, stepA.Object },
            NullLogger<GlobalBootstrapOrchestrator>.Instance
        );

        await orchestrator.RunAsync(CancellationToken.None);

        executionLog
            .Should()
            .Equal(
                new List<int> { 10, 20 },
                "el orden de ejecución debe ser explícito por Order, no el de registro en DI"
            );
    }

    [Fact]
    public async Task Sin_steps_registrados_no_falla_y_no_ejecuta_nada()
    {
        var orchestrator = new GlobalBootstrapOrchestrator(
            Array.Empty<IGlobalBootstrapStep>(),
            NullLogger<GlobalBootstrapOrchestrator>.Instance
        );

        var act = async () => await orchestrator.RunAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task El_orquestador_es_idempotente_invocar_dos_veces_ejecuta_cada_step_dos_veces_sin_error()
    {
        var stepA = MakeStep(10, new List<int>());
        var stepB = MakeStep(20, new List<int>());

        var orchestrator = new GlobalBootstrapOrchestrator(
            new[] { stepA.Object, stepB.Object },
            NullLogger<GlobalBootstrapOrchestrator>.Instance
        );

        await orchestrator.RunAsync(CancellationToken.None);
        await orchestrator.RunAsync(CancellationToken.None);

        stepA.Verify(s => s.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        stepB.Verify(s => s.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
