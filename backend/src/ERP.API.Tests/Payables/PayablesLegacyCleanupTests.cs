using System.Reflection;
using FluentAssertions;

namespace ERP.API.Tests.Payables;

/// <summary>
/// PAYABLES-LEGACY-CLEANUP-13 — guards de regresión: el flujo legacy de CxP exclusivo de Compras
/// (<c>PurchasePayablesController</c>, <c>/api/v1/purchase-payables</c>) fue eliminado por
/// completo en favor de la API genérica <c>PayablesController</c>/<c>/api/v1/payables</c>
/// (Compras + Gastos, vía <c>AccountsPayable</c>). Si alguien reintrodujera el controller legacy,
/// estos tests fallarían.
/// </summary>
public sealed class PayablesLegacyCleanupTests
{
    [Fact]
    public void No_existe_el_controller_legacy_PurchasePayablesController()
    {
        var apiAssembly = typeof(ERP.API.Controllers.PayablesController).Assembly;
        var offending = apiAssembly.GetTypes().Where(t => t.Name == "PurchasePayablesController").ToList();

        offending.Should().BeEmpty();
    }

    [Fact]
    public void PayablesController_es_la_unica_pantalla_de_lectura_de_CxP()
    {
        var apiAssembly = typeof(ERP.API.Controllers.PayablesController).Assembly;
        var controllersEndingInPayablesController = apiAssembly
            .GetTypes()
            .Where(t => t.IsClass && t.Name.EndsWith("PayablesController", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToList();

        controllersEndingInPayablesController.Should().ContainSingle().Which.Should().Be("PayablesController");
    }
}
