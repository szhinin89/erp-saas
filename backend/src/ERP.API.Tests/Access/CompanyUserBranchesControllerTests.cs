using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.GetCompanyUserBranchesAdmin;
using ERP.Application.Access.UseCases.UpdateCompanyUserBranchesAdmin;
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
/// Fase I-B: contrato de CompanyUserBranchesController con StubMediator — mismo alcance que
/// CompanyUserPreferencesControllerTests (Fase F) y CompanyUserMembershipsControllerTests (Fase
/// I-A): mapeo de Command/Query a HTTP y verificación por reflexión de la policy declarada. El
/// body de PUT (UpdateCompanyUserBranchesRequest) no tiene MembershipId/TenantId/CompanyId — el
/// membershipId siempre viene de la ruta.
/// </summary>
public sealed class CompanyUserBranchesControllerTests
{
    private static CompanyUserBranchesController BuildController(Func<object, object> handler)
    {
        var controller = new CompanyUserBranchesController(new StubMediator(handler));
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
        var attr = typeof(CompanyUserBranchesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{AccessPermissions.MembershipsView}");
    }

    // ── GET .../branches ────────────────────────────────────────────────────

    [Fact]
    public async Task Get_retorna_200_y_envia_la_query_correcta_desde_la_ruta()
    {
        var membershipId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<CompanyUserBranchesAdminDto?>.Success(
                new CompanyUserBranchesAdminDto(membershipId, new[] { new CompanyUserBranchOptionDto(branchId, "Matriz", true) }));
        });

        var response = await controller.Get(membershipId, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new GetCompanyUserBranchesAdminQuery(membershipId));
    }

    [Fact]
    public async Task Get_de_membresia_inexistente_o_de_otra_empresa_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<CompanyUserBranchesAdminDto?>.NotFound("Usuario de empresa no encontrado."));

        var response = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── PUT .../branches ────────────────────────────────────────────────────

    [Fact]
    public async Task Update_exitoso_retorna_200_y_envia_el_command_con_el_membershipId_de_la_ruta()
    {
        var membershipId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<CompanyUserBranchesAdminDto>.Success(
                new CompanyUserBranchesAdminDto(membershipId, new[] { new CompanyUserBranchOptionDto(branchId, "Matriz", true) }));
        });

        var response = await controller.Update(
            membershipId, new UpdateCompanyUserBranchesRequest(new[] { branchId }), CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        var sentCommand = sentRequest.Should().BeOfType<UpdateCompanyUserBranchesAdminCommand>().Subject;
        sentCommand.CompanyUserMembershipId.Should().Be(membershipId);
        sentCommand.AuthorizedBranchIds.Should().Equal(branchId);
    }

    [Fact]
    public async Task Update_con_sucursal_inexistente_o_de_otra_empresa_retorna_422()
    {
        var controller = BuildController(_ =>
            Result<CompanyUserBranchesAdminDto>.ValidationFailure("La sucursal no existe o no pertenece a la empresa."));

        var response = await controller.Update(
            Guid.NewGuid(), new UpdateCompanyUserBranchesRequest(new[] { Guid.NewGuid() }), CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task Update_de_membresia_de_otra_empresa_retorna_404()
    {
        var controller = BuildController(_ =>
            Result<CompanyUserBranchesAdminDto>.NotFound("Usuario de empresa no encontrado."));

        var response = await controller.Update(
            Guid.NewGuid(), new UpdateCompanyUserBranchesRequest(Array.Empty<Guid>()), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Update_acepta_lista_vacia()
    {
        var membershipId = Guid.NewGuid();
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<CompanyUserBranchesAdminDto>.Success(
                new CompanyUserBranchesAdminDto(membershipId, Array.Empty<CompanyUserBranchOptionDto>()));
        });

        var response = await controller.Update(
            membershipId, new UpdateCompanyUserBranchesRequest(Array.Empty<Guid>()), CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        var sentCommand = sentRequest.Should().BeOfType<UpdateCompanyUserBranchesAdminCommand>().Subject;
        sentCommand.AuthorizedBranchIds.Should().BeEmpty();
    }
}
