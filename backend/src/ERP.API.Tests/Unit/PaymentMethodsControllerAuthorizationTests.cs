using ERP.API.Controllers;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace ERP.API.Tests.Unit;

/// <summary>
/// DESTINOS-CONTABLES-COBROS-VENTAS-01 (permisos) — "Cobros de ventas" (configuración de
/// PaymentMethodAccount) dejó de depender de SalesPermissions.View/Update: la pantalla vive bajo
/// Contabilidad, no Ventas. Test por reflexión (mismo patrón que
/// <c>CatalogControllerSriLookupAuthorizationTests</c>): el mecanismo real de permisos
/// (RuntimePermissionAuthorizer) no distingue en el harness HTTP sin sembrar tenant/empresa/perfil
/// real, así que la regla se protege aquí, a nivel de atributo, contra que alguien reintroduzca
/// SalesPermissions en las acciones que ahora son exclusivamente de esta pantalla contable.
/// </summary>
public sealed class PaymentMethodsControllerAuthorizationTests
{
    private static AuthorizeAttribute GetAuthorize(string methodName)
    {
        var method = typeof(PaymentMethodsController).GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
        );
        method.Should().NotBeNull($"{methodName} debe existir en PaymentMethodsController");

        var authorize = method!.GetCustomAttribute<AuthorizeAttribute>();
        authorize.Should().NotBeNull($"{methodName} debe seguir requiriendo sesión autenticada");
        return authorize!;
    }

    [Theory]
    [InlineData(nameof(PaymentMethodsController.GetAll))]
    [InlineData(nameof(PaymentMethodsController.GetById))]
    public void Lookup_compartido_no_exige_permiso_de_negocio_especifico(string methodName)
    {
        // Lo consume tanto Ventas/POS (selector de forma de pago) como Contabilidad ("Cobros de
        // ventas") — ninguno de los dos permisos de dominio puede ser el único requisito sin
        // romper al otro consumidor. Mismo patrón que los lookups SRI de solo lectura en
        // CatalogController: [Authorize] simple, sin Policy.
        var authorize = GetAuthorize(methodName);
        authorize.Policy.Should().BeNull($"{methodName} es un lookup compartido de solo lectura");
    }

    [Theory]
    [InlineData(nameof(PaymentMethodsController.Toggle))]
    [InlineData(nameof(PaymentMethodsController.SetAccount))]
    public void Acciones_exclusivas_de_Cobros_de_ventas_exigen_permiso_contable_dedicado(
        string methodName
    )
    {
        var authorize = GetAuthorize(methodName);
        authorize.Policy
            .Should()
            .Be($"perm:{AccountingPermissions.DestinationsSalesCollectionsUpdate}");
        authorize.Policy
            .Should()
            .NotBe(
                $"perm:{SalesPermissions.Update}",
                $"{methodName} ya no debe depender de SalesPermissions"
            );
    }

    [Theory]
    [InlineData(nameof(PaymentMethodsController.Create))]
    [InlineData(nameof(PaymentMethodsController.Update))]
    public void Catalogo_de_metodos_de_pago_sigue_siendo_gestion_comercial_sin_tocar(
        string methodName
    )
    {
        // Fuera de alcance de este ticket: Create/Update (alta/edición del catálogo tenant-wide
        // PaymentMethod — nombre/código/orden/SRI) no las llama la pantalla "Cobros de ventas"
        // (que solo lista, activa/desactiva y asigna cuenta). Siguen siendo un concepto comercial
        // de Ventas, no de Contabilidad — se deja fijado explícitamente para que un cambio futuro
        // no las mueva por accidente al tocar este archivo.
        var expectedPermission = methodName == nameof(PaymentMethodsController.Create)
            ? SalesPermissions.Create
            : SalesPermissions.Update;
        var authorize = GetAuthorize(methodName);
        authorize.Policy.Should().Be($"perm:{expectedPermission}");
    }
}
