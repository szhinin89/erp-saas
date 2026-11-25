using System.Reflection;
using System.Security.Claims;
using ERP.API.Attributes;
using ERP.API.Authorization;
using ERP.API.Controllers;
using ERP.Application.Access.Authorization;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests;

/// <summary>
/// EMP-E4 (T-10) — cada endpoint de Empleados exige su permiso <c>perm:</c> propio y ninguno
/// concede otro. Evalúa las policies declaradas con el proveedor y los handlers reales
/// (sin HTTP ni base de datos). Los menús y el módulo de Kernel llegan con EMP-E7: aquí se
/// comprueba además que el controller no declara <c>[AppFeature]</c> y que no existe DELETE.
/// </summary>
public sealed class EmployeesPermissionContractTests
{
    private static readonly string[] AllEmployeesPermissions =
    [
        EmployeesPermissions.View,
        EmployeesPermissions.Create,
        EmployeesPermissions.Update,
        EmployeesPermissions.Disable,
        EmployeesPermissions.AssignmentsManage,
        EmployeesPermissions.LinkUser,
        EmployeesPermissions.CatalogsView,
        EmployeesPermissions.CatalogsManage,
    ];

    private static readonly (Type Controller, string Action, string Permission)[] Expected =
    [
        (typeof(EmployeesController), nameof(EmployeesController.List), EmployeesPermissions.View),
        (typeof(EmployeesController), nameof(EmployeesController.GetById), EmployeesPermissions.View),
        (typeof(EmployeesController), nameof(EmployeesController.Create), EmployeesPermissions.Create),
        (typeof(EmployeesController), nameof(EmployeesController.Update), EmployeesPermissions.Update),
        (typeof(EmployeesController), nameof(EmployeesController.SetSpecialties), EmployeesPermissions.Update),
        (typeof(EmployeesController), nameof(EmployeesController.Terminate), EmployeesPermissions.Disable),
        (typeof(EmployeesController), nameof(EmployeesController.Rehire), EmployeesPermissions.Disable),
        (typeof(EmployeesController), nameof(EmployeesController.LinkUser), EmployeesPermissions.LinkUser),
        (typeof(EmployeesController), nameof(EmployeesController.UnlinkUser), EmployeesPermissions.LinkUser),
        (typeof(EmployeeBranchAssignmentsController), nameof(EmployeeBranchAssignmentsController.List), EmployeesPermissions.View),
        (typeof(EmployeeBranchAssignmentsController), nameof(EmployeeBranchAssignmentsController.Create), EmployeesPermissions.AssignmentsManage),
        (typeof(EmployeeBranchAssignmentsController), nameof(EmployeeBranchAssignmentsController.Close), EmployeesPermissions.AssignmentsManage),
        (typeof(EmployeeCatalogsController), nameof(EmployeeCatalogsController.List), EmployeesPermissions.CatalogsView),
        (typeof(EmployeeCatalogsController), nameof(EmployeeCatalogsController.GetById), EmployeesPermissions.CatalogsView),
        (typeof(EmployeeCatalogsController), nameof(EmployeeCatalogsController.Create), EmployeesPermissions.CatalogsManage),
        (typeof(EmployeeCatalogsController), nameof(EmployeeCatalogsController.Rename), EmployeesPermissions.CatalogsManage),
        (typeof(EmployeeCatalogsController), nameof(EmployeeCatalogsController.Activate), EmployeesPermissions.CatalogsManage),
        (typeof(EmployeeCatalogsController), nameof(EmployeeCatalogsController.Deactivate), EmployeesPermissions.CatalogsManage),
    ];

    public static TheoryData<string, string> Endpoints
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var (controller, action, _) in Expected)
                data.Add(controller.Name, action);
            return data;
        }
    }

    /// <summary>Vincular exige además consultar membresías (E1, decisión 6); el resto, un solo permiso.</summary>
    private static string[] RequiredFor(Type controller, string action, string permission) =>
        controller == typeof(EmployeesController) && action == nameof(EmployeesController.LinkUser)
            ? [permission, AccessPermissions.MembershipsView]
            : [permission];

    private static (Type Controller, string Action, string Permission) Find(string controller, string action) =>
        Expected.Single(e => e.Controller.Name == controller && e.Action == action);

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Cada_endpoint_exige_exactamente_su_permiso_T10(string controllerName, string action)
    {
        var (controller, _, permission) = Find(controllerName, action);

        (await AuthorizeAsync(controller, action, [])).Succeeded.Should().BeFalse("sin permisos");
        (await AuthorizeAsync(controller, action, AllEmployeesPermissions.Where(p => p != permission).ToArray()))
            .Succeeded.Should().BeFalse("los demás permisos de Empleados no lo habilitan");
        (await AuthorizeAsync(controller, action, [SettingsPermissions.CompaniesUpdate]))
            .Succeeded.Should().BeFalse("un permiso ajeno no lo habilita");
        var required = RequiredFor(controller, action, permission);
        if (required.Length > 1)
            foreach (var single in required)
                (await AuthorizeAsync(controller, action, [single])).Succeeded.Should().BeFalse("exige ambos permisos");
        (await AuthorizeAsync(controller, action, required)).Succeeded.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public void Cada_endpoint_declara_su_policy_perm_y_no_es_anonimo(string controllerName, string action)
    {
        var (controller, _, permission) = Find(controllerName, action);
        var method = controller.GetMethod(action)!;

        method.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy)
            .Should().BeEquivalentTo(RequiredFor(controller, action, permission).Select(p => $"perm:{p}"));
        method.GetCustomAttributes<AllowAnonymousAttribute>().Should().BeEmpty();
        controller.GetCustomAttributes<AllowAnonymousAttribute>().Should().BeEmpty();
        controller.GetCustomAttributes<AuthorizeAttribute>().Should().Contain(a => a.Policy == null,
            "autenticación a nivel de controller");
    }

    [Theory]
    [InlineData(typeof(EmployeesController))]
    [InlineData(typeof(EmployeeCatalogsController))]
    [InlineData(typeof(EmployeeBranchAssignmentsController))]
    public void Los_controllers_cubren_todas_sus_acciones_no_declaran_menu_ni_DELETE(Type controller)
    {
        var actions = controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet();
        actions.Should().BeEquivalentTo(Expected.Where(e => e.Controller == controller).Select(e => e.Action));

        controller.GetCustomAttributes<AppFeatureAttribute>().Should().BeEmpty("el menú es EMP-E7");
        controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods)
            .Should().NotContain("DELETE");
    }

    [Fact]
    public void Los_controllers_son_delgados_solo_dependen_de_IMediator()
    {
        foreach (var controller in new[] { typeof(EmployeesController), typeof(EmployeeCatalogsController), typeof(EmployeeBranchAssignmentsController) })
            controller.GetConstructors().Single().GetParameters()
                .Select(p => p.ParameterType.Name).Should().Equal("IMediator");
    }

    private static async Task<AuthorizationResult> AuthorizeAsync(Type controller, string action, string[] granted)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, HasTenantHandler>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddSingleton<IRuntimePermissionAuthorizer>(new GrantedKeysAuthorizer(granted));
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Combina [Authorize] de clase y de acción como lo hace el endpoint routing.
        var authorizeData = controller
            .GetCustomAttributes<AuthorizeAttribute>()
            .Concat(controller.GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>())
            .ToList();
        var policy = await AuthorizationPolicy.CombineAsync(
            scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>(),
            authorizeData
        );

        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Role, "User"),
                    new Claim("tenant_id", Guid.NewGuid().ToString()),
                },
                authenticationType: "Test"
            )
        );

        return await scope
            .ServiceProvider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(user, resource: null, policy!);
    }

    private sealed class GrantedKeysAuthorizer(string[] granted) : IRuntimePermissionAuthorizer
    {
        public Task<bool> IsAuthorizedAsync(
            string permissionKey,
            Guid userId,
            string role,
            CancellationToken cancellationToken
        ) => Task.FromResult(granted.Contains(permissionKey, StringComparer.Ordinal));
    }
}
