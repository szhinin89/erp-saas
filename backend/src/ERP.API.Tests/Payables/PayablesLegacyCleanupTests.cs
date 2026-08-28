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

    /// <summary>
    /// SUPPLIER-PAYMENTS-REGISTER-15C — <c>SupplierPaymentsController</c> es el único endpoint de
    /// escritura para pagos a proveedores; no debe existir un endpoint legacy
    /// <c>POST /api/v1/finance/payments</c> (o similar) que registre pagos AP fuera de
    /// <c>SupplierPayment</c>. <c>FinancePaymentsController</c> sigue existiendo solo para Collections
    /// (AR) — <c>RegisterCollection</c>.
    /// </summary>
    [Fact]
    public void No_existe_endpoint_legacy_de_registro_de_pago_a_proveedor()
    {
        var apiAssembly = typeof(ERP.API.Controllers.PayablesController).Assembly;
        var financeController = apiAssembly
            .GetTypes()
            .Single(t => t.Name == "FinancePaymentsController");

        var methodNames = financeController
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();

        methodNames.Should().NotContain(new[] { "RegisterPayment", "ReversePayment" });
    }

    /// <summary>
    /// SUPPLIER-PAYMENTS-REGISTER-15C / SUPPLIER-PAYMENTS-FRONTEND-15E — <c>SupplierPaymentsController</c>
    /// expone exactamente los endpoints esperados (registro + lectura mínima para el frontend), en
    /// la ruta genérica <c>/api/v1/supplier-payments</c> — nunca anidado bajo <c>/finance/</c> ni bajo
    /// <c>/payables/</c> (que sigue siendo solo lectura de <c>AccountsPayable</c>). Ningún endpoint de
    /// edición/reversa todavía — "sin Draft, sin edición posterior".
    /// </summary>
    [Fact]
    public void SupplierPaymentsController_expone_unicamente_registro_y_lectura()
    {
        var apiAssembly = typeof(ERP.API.Controllers.PayablesController).Assembly;
        var controller = apiAssembly.GetTypes().Single(t => t.Name == "SupplierPaymentsController");

        var route = controller
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RouteAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Mvc.RouteAttribute>()
            .Single();
        route.Template.Should().Be("api/v1/supplier-payments");

        var publicMethods = controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();
        publicMethods.Should().BeEquivalentTo(new[] { "Register", "GetById", "GetList" });
    }
}
