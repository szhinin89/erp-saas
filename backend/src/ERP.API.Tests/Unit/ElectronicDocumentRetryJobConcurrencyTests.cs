using ERP.API.Hangfire;
using FluentAssertions;
using Hangfire;

namespace ERP.API.Tests.Unit;

/// <summary>
/// A1 (auditoría de robustez): el job de reintento corre cada minuto (cron en Program.cs) sin
/// ningún guard de ejecución concurrente. Este test no reemplaza una prueba de integración con
/// storage real de Hangfire (el guard usa un lock distribuido que solo se ejerce con storage
/// real) — verifica por reflexión que el atributo declarativo está aplicado, que es lo único
/// verificable sin levantar infraestructura Hangfire completa.
/// </summary>
public sealed class ElectronicDocumentRetryJobConcurrencyTests
{
    [Fact]
    public void ExecuteAsync_has_DisableConcurrentExecution_attribute()
    {
        var method = typeof(ElectronicDocumentRetryJob).GetMethod(nameof(ElectronicDocumentRetryJob.ExecuteAsync));

        method.Should().NotBeNull();
        var attribute = method!.GetCustomAttributes(typeof(DisableConcurrentExecutionAttribute), inherit: false)
            .Cast<DisableConcurrentExecutionAttribute>()
            .SingleOrDefault();

        attribute.Should().NotBeNull(
            "el job corre cada minuto (Program.cs) y una corrida que exceda 60s no debe solaparse con la siguiente");
    }
}
