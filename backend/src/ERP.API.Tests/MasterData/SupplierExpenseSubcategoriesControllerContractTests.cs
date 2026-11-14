using ERP.API.Controllers;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace ERP.API.Tests.MasterData;

/// <summary>
/// EXPENSE-SUPPLIER-SUBCATEGORIES-01 — contrato de ruta y permisos: se reutilizan los permisos de
/// configuración de proveedor por empresa (lectura = view, escritura = configure-company); no se
/// crea ningún permiso nuevo y no existe DELETE.
/// </summary>
public sealed class SupplierExpenseSubcategoriesControllerContractTests
{
    private static readonly Type Controller = typeof(SupplierExpenseSubcategoriesController);

    [Fact]
    public void Ruta_bajo_el_proveedor_y_sesion_obligatoria()
    {
        Controller.GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>().Single().Template
            .Should().Be("api/v1/master/business-partners/{bpId:guid}/expense-subcategories");
        Controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().Contain(a => a.Policy == "Session");
    }

    [Theory]
    [InlineData(nameof(SupplierExpenseSubcategoriesController.GetExpenseSubcategories), typeof(HttpGetAttribute), MasterDataPermissions.BusinessPartnersView)]
    [InlineData(nameof(SupplierExpenseSubcategoriesController.SetExpenseSubcategories), typeof(HttpPutAttribute), MasterDataPermissions.BusinessPartnersConfigureCompany)]
    public void Cada_accion_exige_el_permiso_existente_que_le_corresponde(string action, Type verb, string permission)
    {
        var method = Controller.GetMethod(action)!;
        method.GetCustomAttributes(verb, true).Should().ContainSingle();
        method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().Contain(a => a.Policy == $"perm:{permission}");
    }

    [Fact]
    public void No_expone_DELETE_ni_otras_acciones_de_escritura()
    {
        Controller.GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true))
            .Cast<HttpMethodAttribute>().SelectMany(a => a.HttpMethods).Should().BeEquivalentTo(["GET", "PUT"]);
    }
}
