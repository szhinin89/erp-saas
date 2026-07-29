using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// El orquestador no debe contener lógica de negocio — solo descubrir, ordenar y ejecutar los
/// ICompanyBootstrapStep registrados en DI. Estas pruebas cubren exactamente esa responsabilidad:
/// orden explícito (nunca el de registro) y ejecución completa de todos los steps.
/// </summary>
public sealed class CompanyBootstrapOrchestratorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static Mock<ICompanyBootstrapStep> MakeStep(int order, List<int> executionLog)
    {
        var step = new Mock<ICompanyBootstrapStep>();
        step.Setup(s => s.Order).Returns(order);
        step.Setup(s => s.ExecuteAsync(It.IsAny<CompanyBootstrapContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => executionLog.Add(order));
        return step;
    }

    [Fact]
    public async Task Ejecuta_los_steps_en_orden_ascendente_por_Order_sin_importar_el_orden_de_registro()
    {
        var executionLog = new List<int>();

        // Registrados deliberadamente en desorden — el orquestador debe reordenar por Order.
        var stepC = MakeStep(30, executionLog);
        var stepA = MakeStep(10, executionLog);
        var stepB = MakeStep(20, executionLog);

        var orchestrator = new CompanyBootstrapOrchestrator(
            new[] { stepC.Object, stepA.Object, stepB.Object },
            NullLogger<CompanyBootstrapOrchestrator>.Instance);

        await orchestrator.BootstrapCompanyAsync(TenantId, CompanyId, ActorId, CancellationToken.None);

        executionLog.Should().Equal(new List<int> { 10, 20, 30 },
            "el orden de ejecución debe ser explícito por Order, no el de registro en DI");
    }

    [Fact]
    public async Task Ejecuta_todos_los_steps_registrados_exactamente_una_vez_con_el_contexto_correcto()
    {
        var steps = Enumerable.Range(1, 5)
            .Select(i => MakeStep(i * 10, new List<int>()))
            .ToList();

        var orchestrator = new CompanyBootstrapOrchestrator(
            steps.Select(s => s.Object),
            NullLogger<CompanyBootstrapOrchestrator>.Instance);

        await orchestrator.BootstrapCompanyAsync(TenantId, CompanyId, ActorId, CancellationToken.None);

        foreach (var step in steps)
        {
            step.Verify(s => s.ExecuteAsync(
                It.Is<CompanyBootstrapContext>(c =>
                    c.TenantId == TenantId && c.CompanyId == CompanyId && c.ActorId == ActorId),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task Sin_steps_registrados_no_falla_y_no_ejecuta_nada()
    {
        var orchestrator = new CompanyBootstrapOrchestrator(
            Array.Empty<ICompanyBootstrapStep>(),
            NullLogger<CompanyBootstrapOrchestrator>.Instance);

        var act = async () => await orchestrator.BootstrapCompanyAsync(TenantId, CompanyId, ActorId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task El_orquestador_es_idempotente_invocar_dos_veces_ejecuta_cada_step_dos_veces_sin_error()
    {
        // El orquestador no mantiene estado propio entre corridas — la idempotencia real vive en
        // cada step (verifica existencia antes de insertar). Esta prueba solo cubre la parte que
        // sí es responsabilidad del orquestador: no debe impedir ni alterar una segunda corrida.
        var stepA = MakeStep(10, new List<int>());
        var stepB = MakeStep(20, new List<int>());

        var orchestrator = new CompanyBootstrapOrchestrator(
            new[] { stepA.Object, stepB.Object },
            NullLogger<CompanyBootstrapOrchestrator>.Instance);

        await orchestrator.BootstrapCompanyAsync(TenantId, CompanyId, ActorId, CancellationToken.None);
        await orchestrator.BootstrapCompanyAsync(TenantId, CompanyId, ActorId, CancellationToken.None);

        stepA.Verify(s => s.ExecuteAsync(It.IsAny<CompanyBootstrapContext>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        stepB.Verify(s => s.ExecuteAsync(It.IsAny<CompanyBootstrapContext>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
