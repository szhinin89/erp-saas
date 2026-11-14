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

    [Fact]
    public void Escritura_exige_configure_company_existente()
    {
        var method = Controller.GetMethod(nameof(SupplierExpenseSubcategoriesController.SetExpenseSubcategories))!;
        method.GetCustomAttributes(typeof(HttpPutAttribute), true).Should().ContainSingle();
        method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().Contain(a => a.Policy == $"perm:{MasterDataPermissions.BusinessPartnersConfigureCompany}");
    }

    [Fact]
    public void Lectura_no_fija_un_permiso_unico_en_el_controller_porque_no_hay_politicas_OR()
    {
        // Ficha del proveedor (businesspartners.view) y captura de gastos (expenses.documents.create|update)
        // pueden leer; la decisión vive en el caso de uso con el autorizador oficial (ver
        // SupplierExpenseSubcategoriesTests). Aquí solo se fija que no quede un perm: único que la bloquee.
        var method = Controller.GetMethod(nameof(SupplierExpenseSubcategoriesController.GetExpenseSubcategories))!;
        method.GetCustomAttributes(typeof(HttpGetAttribute), true).Should().ContainSingle();
        method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().NotContain(a => a.Policy != null && a.Policy.StartsWith("perm:"));
        Controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().Contain(a => a.Policy == "Session", "la sesión sigue siendo obligatoria");
    }

    [Fact]
    public void No_expone_DELETE_ni_otras_acciones_de_escritura()
    {
        Controller.GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true))
            .Cast<HttpMethodAttribute>().SelectMany(a => a.HttpMethods).Should().BeEquivalentTo(["GET", "PUT"]);
    }
}
