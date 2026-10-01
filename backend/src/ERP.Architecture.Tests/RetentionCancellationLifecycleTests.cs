using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// ZH-RETENTION-CANCELLATION-LIFECYCLE-01 — una retención se emite dentro de la confirmación de su
/// documento origen (RETENTIONS-MODULE-DESIGN-01 decisión 15) y solo se anula como consecuencia de
/// anular ese origen; <c>Cancelled</c> es terminal. Una anulación aislada dejaría el origen
/// confirmado sin la retención que se pidió al confirmarlo y sin forma de volver a emitirla. Este gate
/// impide reintroducirla: solo las anulaciones de Compra y Gasto pueden usar <c>IRetentionCanceller</c>.
/// </summary>
public sealed class RetentionCancellationLifecycleTests
{
    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(ERP.Application.DependencyInjection).Assembly;

    private static readonly System.Reflection.Assembly ApiAssembly =
        typeof(ERP.API.Controllers.PurchasesController).Assembly;

    private static readonly string[] AllowedOrchestrators =
    {
        "ERP.Application.Modules.Purchases.UseCases.CancelPurchaseHandler",
        "ERP.Application.Modules.Expenses.UseCases.Documents.CancelExpenseDocumentHandler",
        // Definición, implementación y registro DI del propio servicio.
        "ERP.Application.Modules.Retentions.Services.IRetentionCanceller",
        "ERP.Application.Modules.Retentions.Services.RetentionCanceller",
        "ERP.Application.DependencyInjection",
    };

    [Fact]
    public void Solo_la_anulacion_del_documento_origen_puede_anular_una_retencion()
    {
        var dependents = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .HaveDependencyOn("ERP.Application.Modules.Retentions.Services.IRetentionCanceller")
            .GetTypes()
            .Select(t => t.FullName!.Split('+')[0])
            .Distinct()
            .ToList();

        dependents.Should().OnlyContain(name => AllowedOrchestrators.Contains(name));
        dependents.Should().Contain(AllowedOrchestrators[0]).And.Contain(AllowedOrchestrators[1]);
    }

    [Fact]
    public void Ningun_controller_expone_una_anulacion_aislada_de_retencion()
    {
        var result = Types
            .InAssembly(ApiAssembly)
            .That()
            .ResideInNamespaceStartingWith("ERP.API.Controllers")
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.Retentions.Services.IRetentionCanceller")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
        ApplicationAssembly.GetType("ERP.Application.Modules.Retentions.UseCases.CancelRetentionCommand")
            .Should().BeNull("la retención solo se anula anulando su documento origen");
    }
}
