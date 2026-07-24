using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.CloseUserSessionAdmin;
using ERP.Application.Access.UseCases.GetSessionStatistics;
using ERP.Application.Access.UseCases.GetUserSessionsPaged;
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
/// Fase 10: contrato de AdminUserSessionController con StubMediator — mismo alcance que
/// UserSessionControllerTests (Fase 5): mapeo de Command/Query y de Result&lt;T&gt; a HTTP.
/// La imposición real de las policies de autorización la resuelve el pipeline de ASP.NET Core
/// (PermissionPolicyProvider/PermissionHandler, ya existentes, no reprobados aquí) — lo que SÍ
/// se prueba, por reflexión, es que los atributos [Authorize] declarados son los correctos y
/// que el endpoint de cierre exige un permiso más restrictivo que el resto del controller.
/// </summary>
public sealed class AdminUserSessionControllerTests
{
    private static AdminUserSessionController BuildController(Func<object, object> handler)
    {
        var controller = new AdminUserSessionController(new StubMediator(handler));
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
        };
        return controller;
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ERP.API.Tests";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    // ── Autorización declarativa ─────────────────────────────────────────────

    [Fact]
    public void El_controller_exige_perm_access_sessions_view()
    {
        var attr = typeof(AdminUserSessionController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{AccessPermissions.SessionsView}");
    }

    [Fact]
    public void El_endpoint_de_cierre_exige_un_permiso_mas_restrictivo_que_el_resto_del_controller()
    {
        var closeMethod = typeof(AdminUserSessionController).GetMethod(nameof(AdminUserSessionController.Close))!;
        var attr = closeMethod
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{AccessPermissions.SessionsClose}");
        attr.Policy.Should().NotBe($"perm:{AccessPermissions.SessionsView}");
    }

    // ── GET /api/v1/admin/access/sessions ───────────────────────────────────

    [Fact]
    public async Task GetPaged_retorna_200_y_envia_la_query_correcta()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<PagedResult<UserSessionAdminDto>>.Success(
                new PagedResult<UserSessionAdminDto>(Array.Empty<UserSessionAdminDto>(), 1, 25, 0));
        });

        var identityUserId = Guid.NewGuid();
        var response = await controller.GetPaged(identityUserId, null, "Active", null, null, 1, 25, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new GetUserSessionsPagedQuery(identityUserId, null, "Active", null, null, 1, 25));
    }

    // ── GET /api/v1/admin/access/sessions/statistics ────────────────────────

    [Fact]
    public async Task GetStatistics_retorna_200_y_envia_la_query_correcta()
    {
        object? sentRequest = null;
        var companyId = Guid.NewGuid();
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<SessionStatisticsDto>.Success(new SessionStatisticsDto(1, 2, 3, 4, 10));
        });

        var response = await controller.GetStatistics(companyId, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new GetSessionStatisticsQuery(companyId));
    }

    // ── POST /api/v1/admin/access/sessions/{id}/close ───────────────────────

    [Fact]
    public async Task Close_exitoso_retorna_200_y_envia_el_command_correcto()
    {
        var sessionId = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<string>.Success("Sesión cerrada correctamente.");
        });

        var response = await controller.Close(sessionId, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new CloseUserSessionAdminCommand(sessionId));
    }

    [Fact]
    public async Task Close_sesion_inexistente_retorna_404()
    {
        var controller = BuildController(_ => Result<string>.NotFound("Sesión no encontrada."));

        var response = await controller.Close(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }
}
