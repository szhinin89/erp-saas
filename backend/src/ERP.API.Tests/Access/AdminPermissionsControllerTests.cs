using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Application.Common;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Access;

/// <summary>
/// ADMIN-PERMISSIONS-SSOT-KERNEL-02 — contrato de AdminPermissionsController con StubMediator,
/// mismo patrón que CompanyUserMembershipsControllerTests: mapeo Query→HTTP + verificación por
/// reflexión de la policy declarada. No existía este controller antes de este ticket.
/// </summary>
public sealed class AdminPermissionsControllerTests
{
    private static AdminPermissionsController BuildController(Func<object, object> handler)
    {
        var controller = new AdminPermissionsController(new StubMediator(handler));
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider(),
            },
        };
        return controller;
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ERP.API.Tests";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } =
            null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            null!;
    }

    [Fact]
    public void El_controller_exige_perm_access_profiles_view_reutilizando_el_permiso_existente()
    {
        // No se inventa un permiso nuevo — reutiliza el mismo que ya exigen
        // GET/PUT .../profiles/{id}/permissions en AccessProfilesController.
        var attrs = typeof(AdminPermissionsController)
            .GetMethod(nameof(AdminPermissionsController.GetCatalog))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        attrs.Should().Contain(a => a.Policy == $"perm:{AccessPermissions.ProfilesView}");
    }

    [Fact]
    public async Task GetCatalog_retorna_200_con_el_resultado_del_query()
    {
        object? sentRequest = null;
        var expected = new PermissionCatalogDto(
            new[]
            {
                new PermissionCatalogGroupDto(
                    "admin",
                    "app.nav.group.admin",
                    60,
                    new[]
                    {
                        new PermissionCatalogItemDto(
                            Guid.NewGuid(),
                            "app.nav.item.admin.roles",
                            "/admin/roles",
                            "access.profiles.view",
                            20,
                            new[]
                            {
                                new PermissionCatalogActionDto(
                                    "access.profiles.view",
                                    "Ver / Acceder",
                                    "Permite ver y acceder a esta pantalla.",
                                    0
                                ),
                            }
                        ),
                    }
                ),
            }
        );

        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<PermissionCatalogDto>.Success(expected);
        });

        var response = await controller.GetCatalog(CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().BeOfType<GetPermissionCatalogQuery>();
    }
}
