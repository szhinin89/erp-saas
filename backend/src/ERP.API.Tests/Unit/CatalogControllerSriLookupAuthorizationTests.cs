using ERP.API.Controllers;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace ERP.API.Tests.Unit;

/// <summary>
/// GET sri-tax-support-codes y sri-retention-codes son catálogos SRI de solo lectura para
/// formularios operativos (ej. "Config SRI — Proveedor") — no pantallas de administración.
/// Ya no deben exigir CatalogPermissions.Manage, mismo patrón que sri-payment-methods y
/// sri-supplier-types (ambos [Authorize] simple en este mismo controller). Test por reflexión,
/// sin BD/HTTP: el mecanismo real de permisos (RuntimePermissionAuthorizer) no distingue en el
/// harness de test HTTP entre roles no-Admin sin sembrar tenant/empresa/perfil real, así que la
/// regla se protege aquí, a nivel de atributo, contra que alguien reintroduzca la restricción.
/// </summary>
public sealed class CatalogControllerSriLookupAuthorizationTests
{
    [Theory]
    [InlineData(nameof(CatalogController.GetSriTaxSupportCodes))]
    [InlineData(nameof(CatalogController.GetSriRetentionCodes))]
    public void Endpoint_de_lectura_no_exige_CatalogPermissions_Manage(string methodName)
    {
        var method = typeof(CatalogController).GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
        );
        method.Should().NotBeNull($"{methodName} debe existir en CatalogController");

        var authorize = method!.GetCustomAttribute<AuthorizeAttribute>();
        authorize.Should().NotBeNull($"{methodName} debe seguir requiriendo sesión autenticada");
        authorize!.Policy
            .Should()
            .NotBe(
                $"perm:{CatalogPermissions.Manage}",
                $"{methodName} es un catálogo de lectura, no debe exigir CatalogPermissions.Manage"
            );
    }

    [Theory]
    [InlineData(nameof(CatalogController.GetSriPaymentMethods))]
    [InlineData(nameof(CatalogController.GetSriSupplierTypes))]
    public void Mismo_patron_que_los_otros_lookups_SRI_de_solo_lectura(string methodName)
    {
        var method = typeof(CatalogController).GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
        );

        var authorize = method!.GetCustomAttribute<AuthorizeAttribute>();
        authorize.Should().NotBeNull();
        authorize!.Policy.Should().BeNull(
            $"{methodName} ya usa [Authorize] simple — referencia del patrón a seguir"
        );
    }
}
