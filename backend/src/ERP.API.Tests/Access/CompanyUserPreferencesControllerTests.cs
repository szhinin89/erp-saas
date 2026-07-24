using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.GetCompanyUserPreferencesAdmin;
using ERP.Application.Access.UseCases.UpdateCompanyUserPreferencesAdmin;
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
/// Fase F: contrato de CompanyUserPreferencesController con StubMediator — mismo alcance que
/// AdminUserSessionControllerTests (Fase 10): mapeo de Command/Query y de Result&lt;T&gt; a HTTP,
/// y verificación por reflexión de que la policy de autorización declarada es la correcta (la
/// imposición real de permisos la resuelve el pipeline de ASP.NET Core ya existente, no probado
/// de nuevo aquí).
/// </summary>
public sealed class CompanyUserPreferencesControllerTests
{
    private static CompanyUserPreferencesController BuildController(Func<object, object> handler)
    {
        var controller = new CompanyUserPreferencesController(new StubMediator(handler));
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
    public void El_controller_exige_perm_access_company_user_memberships_view()
    {
        var attr = typeof(CompanyUserPreferencesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{AccessPermissions.MembershipsView}");
    }

    // ── GET .../preferences ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_retorna_200_y_envia_la_query_correcta()
    {
        var companyUserId = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<CompanyUserPreferencesAdminDto?>.Success(
                new CompanyUserPreferencesAdminDto(companyUserId, null, "AskBranch"));
        });

        var response = await controller.Get(companyUserId, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new GetCompanyUserPreferencesAdminQuery(companyUserId));
    }

    [Fact]
    public async Task Get_de_usuario_inexistente_o_de_otra_empresa_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<CompanyUserPreferencesAdminDto?>.NotFound("Usuario de empresa no encontrado."));

        var response = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── PUT .../preferences ──────────────────────────────────────────────────

    [Fact]
    public async Task Update_exitoso_retorna_200_y_envia_el_command_correcto_desde_la_ruta_y_el_body()
    {
        var companyUserId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<CompanyUserPreferencesAdminDto>.Success(
                new CompanyUserPreferencesAdminDto(companyUserId, branchId, "DirectToDefault"));
        });

        var response = await controller.Update(
            companyUserId, new UpdateCompanyUserPreferencesRequest("DirectToDefault", branchId), CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new UpdateCompanyUserPreferencesAdminCommand(companyUserId, "DirectToDefault", branchId));
    }

    [Fact]
    public async Task Update_con_sucursal_no_autorizada_retorna_422()
    {
        var controller = BuildController(_ =>
            Result<CompanyUserPreferencesAdminDto>.ValidationFailure(
                "La sucursal por defecto debe estar previamente autorizada para este usuario (CompanyUserBranch)."));

        var response = await controller.Update(
            Guid.NewGuid(), new UpdateCompanyUserPreferencesRequest("DirectToDefault", Guid.NewGuid()), CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task Update_de_usuario_de_otra_empresa_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<CompanyUserPreferencesAdminDto>.NotFound("Usuario de empresa no encontrado."));

        var response = await controller.Update(
            Guid.NewGuid(), new UpdateCompanyUserPreferencesRequest("AskBranch", null), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }
}
