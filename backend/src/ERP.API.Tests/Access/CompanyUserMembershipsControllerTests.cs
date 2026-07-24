using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.GetCompanyUserMembershipsAdmin;
using ERP.Application.Access.UseCases.LookupUserByUsernameAdmin;
using ERP.Application.Access.UseCases.RevokeCompanyUserMembershipAdmin;
using ERP.Application.Access.UseCases.UpsertCompanyUserMembershipAdmin;
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
/// Fase I-A: contrato de CompanyUserMembershipsController con StubMediator — mismo alcance que
/// CompanyUserPreferencesControllerTests (Fase F): mapeo de Command a HTTP y verificación por
/// reflexión de la policy de autorización declarada. La imposición real de permisos la resuelve el
/// pipeline de ASP.NET Core ya existente, no probado de nuevo aquí. Los request DTOs
/// (UpsertCompanyUserMembershipRequest/RevokeCompanyUserMembershipRequest) no exponen ningún campo
/// TenantId/CompanyId — el test de mapping abajo confirma que el Command enviado a MediatR nunca
/// incluye esos valores más allá de lo que el propio Admin command ya no acepta.
/// </summary>
public sealed class CompanyUserMembershipsControllerTests
{
    private static CompanyUserMembershipsController BuildController(Func<object, object> handler)
    {
        var controller = new CompanyUserMembershipsController(new StubMediator(handler));
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
        var attr = typeof(CompanyUserMembershipsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{AccessPermissions.MembershipsView}");
    }

    // ── GET (list) ──────────────────────────────────────────────────────────

    [Fact]
    public async Task List_retorna_200_y_envia_onlyActive_desde_el_query_string()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<IReadOnlyList<CompanyUserMembershipAdminDto>>.Success(
                new[] { new CompanyUserMembershipAdminDto(Guid.NewGuid(), Guid.NewGuid(), "ana.perez", "Ana Perez", "ana@test.com", "User", true, null, null) });
        });

        var response = await controller.List(onlyActive: true, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new GetCompanyUserMembershipsAdminQuery(true));
    }

    [Fact]
    public async Task List_default_onlyActive_false()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<IReadOnlyList<CompanyUserMembershipAdminDto>>.Success(Array.Empty<CompanyUserMembershipAdminDto>());
        });

        await controller.List(cancellationToken: CancellationToken.None);

        sentRequest.Should().Be(new GetCompanyUserMembershipsAdminQuery(false));
    }

    [Fact]
    public async Task List_sin_empresa_activa_retorna_403()
    {
        var controller = BuildController(_ =>
            Result<IReadOnlyList<CompanyUserMembershipAdminDto>>.Forbidden("No hay una empresa activa en la sesión."));

        var response = await controller.List(cancellationToken: CancellationToken.None);

        response.Should().BeOfType<ObjectResult>();
        ((ObjectResult)response).StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // ── GET lookup (Fase F) ────────────────────────────────────────────────

    [Fact]
    public async Task Lookup_envia_el_username_recibido_por_query_string()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<UsernameLookupDto>.Success(new UsernameLookupDto(false, null, null));
        });

        var response = await controller.Lookup("ana.perez", CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new LookupUserByUsernameAdminQuery("ana.perez"));
    }

    [Fact]
    public async Task Lookup_sin_empresa_activa_retorna_403()
    {
        var controller = BuildController(_ =>
            Result<UsernameLookupDto>.Forbidden("No hay una empresa activa en la sesión."));

        var response = await controller.Lookup("ana.perez", CancellationToken.None);

        response.Should().BeOfType<ObjectResult>();
        ((ObjectResult)response).StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // ── POST (upsert) ───────────────────────────────────────────────────────

    [Fact]
    public async Task Upsert_exitoso_retorna_200_y_envia_el_command_sin_TenantId_ni_CompanyId_en_el_contrato()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<object>.Success(new { });
        });

        var branchId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var response = await controller.Upsert(
            new UpsertCompanyUserMembershipRequest("ana.perez", "Admin", profileId, new[] { branchId }, branchId, "DirectToDefault"),
            CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        var sentCommand = sentRequest.Should().BeOfType<UpsertCompanyUserMembershipAdminCommand>().Subject;
        sentCommand.Username.Should().Be("ana.perez");
        sentCommand.Role.Should().Be("Admin");
        sentCommand.ProfileId.Should().Be(profileId);
        sentCommand.AuthorizedBranchIds.Should().Equal(branchId);
        sentCommand.DefaultBranchId.Should().Be(branchId);
        sentCommand.LoginMode.Should().Be("DirectToDefault");
    }

    [Fact]
    public async Task Upsert_con_empresa_activa_distinta_retorna_403()
    {
        var controller = BuildController(_ =>
            Result<object>.Forbidden("La empresa activa no coincide con el contexto administrado."));

        var response = await controller.Upsert(
            new UpsertCompanyUserMembershipRequest("ana.perez", "User"), CancellationToken.None);

        response.Should().BeOfType<ObjectResult>();
        ((ObjectResult)response).StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Upsert_con_usuario_inexistente_retorna_400()
    {
        var controller = BuildController(_ => Result<object>.Failure("Usuario no existe."));

        var response = await controller.Upsert(
            new UpsertCompanyUserMembershipRequest("no-existe", "User"), CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Upsert_con_sucursal_no_autorizada_retorna_422()
    {
        var controller = BuildController(_ =>
            Result<object>.ValidationFailure(
                "La sucursal por defecto debe estar previamente autorizada para este usuario (CompanyUserBranch)."));

        var response = await controller.Upsert(
            new UpsertCompanyUserMembershipRequest("ana.perez", "User"), CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    // ── POST revoke ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Revoke_exitoso_retorna_200_y_envia_el_command_sin_TenantId_ni_CompanyId_en_el_contrato()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<object>.Success(new { });
        });

        var response = await controller.Revoke(
            new RevokeCompanyUserMembershipRequest("ana.perez"), CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new RevokeCompanyUserMembershipAdminCommand("ana.perez"));
    }

    [Fact]
    public async Task Revoke_con_empresa_activa_distinta_retorna_403()
    {
        var controller = BuildController(_ =>
            Result<object>.Forbidden("La empresa activa no coincide con el contexto administrado."));

        var response = await controller.Revoke(
            new RevokeCompanyUserMembershipRequest("ana.perez"), CancellationToken.None);

        response.Should().BeOfType<ObjectResult>();
        ((ObjectResult)response).StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Revoke_con_usuario_inexistente_retorna_400()
    {
        var controller = BuildController(_ => Result<object>.Failure("Usuario no existe."));

        var response = await controller.Revoke(
            new RevokeCompanyUserMembershipRequest("no-existe"), CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>();
    }
}
