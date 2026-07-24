using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Access.DTOs;
using ERP.Application.Access.UseCases.AssignTemporaryPasswordAdmin;
using ERP.Application.Access.UseCases.CreateSystemUserAdmin;
using ERP.Application.Common;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ERP.API.Tests.Access;

/// <summary>
/// Contrato de IdentityUsersController con StubMediator — mismo alcance que
/// CompanyUserMembershipsControllerTests: mapeo de Command a HTTP y verificación por reflexión de
/// la policy de autorización declarada (permiso propio IdentityUsersCreate, no MembershipsView —
/// confirma que no quedó acoplado por AND al permiso de membership). La imposición real de permisos
/// la resuelve el pipeline de ASP.NET Core ya existente, no probado de nuevo aquí.
/// </summary>
public sealed class IdentityUsersControllerTests
{
    private static IdentityUsersController BuildController(Func<object, object> handler)
    {
        var controller = new IdentityUsersController(new StubMediator(handler));
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

    [Fact]
    public void El_controller_exige_perm_access_identity_users_create()
    {
        var attr = typeof(IdentityUsersController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{AccessPermissions.IdentityUsersCreate}");
    }

    [Fact]
    public async Task Create_exitoso_retorna_200_y_envia_el_command_sin_TenantId_ni_CompanyId_en_el_contrato()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<CreateSystemUserResultDto>.Success(new CreateSystemUserResultDto(Guid.NewGuid(), "ana.perez"));
        });

        var profileId = Guid.NewGuid();
        var response = await controller.Create(
            new CreateSystemUserRequest("ana.perez", "Ana", "Perez", "ana@test.com", "S3curePass!", "Admin", profileId),
            CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        var sentCommand = sentRequest.Should().BeOfType<CreateSystemUserAdminCommand>().Subject;
        sentCommand.Username.Should().Be("ana.perez");
        sentCommand.FirstName.Should().Be("Ana");
        sentCommand.LastName.Should().Be("Perez");
        sentCommand.Email.Should().Be("ana@test.com");
        sentCommand.Role.Should().Be("Admin");
        sentCommand.ProfileId.Should().Be(profileId);
    }

    [Fact]
    public async Task Create_con_empresa_activa_distinta_retorna_403()
    {
        var controller = BuildController(_ =>
            Result<CreateSystemUserResultDto>.Forbidden("La empresa activa no coincide con el contexto administrado."));

        var response = await controller.Create(
            new CreateSystemUserRequest("ana.perez", "Ana", "Perez", "ana@test.com", "S3curePass!", "User"), CancellationToken.None);

        response.Should().BeOfType<ObjectResult>();
        ((ObjectResult)response).StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Create_con_email_duplicado_retorna_409()
    {
        var controller = BuildController(_ =>
            Result<CreateSystemUserResultDto>.Conflict("Ya existe un usuario con ese email."));

        var response = await controller.Create(
            new CreateSystemUserRequest("ana.perez", "Ana", "Perez", "ana@test.com", "S3curePass!", "User"), CancellationToken.None);

        response.Should().BeOfType<ConflictObjectResult>();
    }

    // ── AssignTemporaryPassword ──────────────────────────────────────────────────────────────

    private static MethodInfo AssignTemporaryPasswordMethod =>
        typeof(IdentityUsersController).GetMethod(nameof(IdentityUsersController.AssignTemporaryPassword))!;

    [Fact]
    public void El_endpoint_exige_perm_access_identity_users_assign_temporary_password()
    {
        var attr = AssignTemporaryPasswordMethod
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be($"perm:{AccessPermissions.IdentityUsersAssignTemporaryPassword}");
    }

    [Fact]
    public void El_endpoint_esta_mapeado_a_POST_username_assign_temporary_password()
    {
        var attr = AssignTemporaryPasswordMethod
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>()
            .Single();

        attr.HttpMethods.Should().ContainSingle().Which.Should().Be("POST");
        attr.Template.Should().Be("{username}/assign-temporary-password");
    }

    [Fact]
    public async Task AssignTemporaryPassword_exitoso_envia_el_Command_con_username_de_ruta_y_password_del_body()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<string>.Success("Contraseña temporal asignada correctamente.");
        });

        var response = await controller.AssignTemporaryPassword(
            "ana.perez", new AssignTemporaryPasswordRequest("Temp0ral!"), CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        var sentCommand = sentRequest.Should().BeOfType<AssignTemporaryPasswordAdminCommand>().Subject;
        sentCommand.Username.Should().Be("ana.perez");
        sentCommand.TemporaryPassword.Should().Be("Temp0ral!");
    }

    [Fact]
    public async Task AssignTemporaryPassword_nunca_devuelve_la_contrasena_en_la_respuesta()
    {
        var controller = BuildController(_ => Result<string>.Success("Contraseña temporal asignada correctamente."));

        var response = await controller.AssignTemporaryPassword(
            "ana.perez", new AssignTemporaryPasswordRequest("Temp0ral!"), CancellationToken.None);

        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
        ok.Value!.ToString().Should().NotContain("Temp0ral!");
    }

    [Fact]
    public async Task AssignTemporaryPassword_usuario_no_encontrado_retorna_404()
    {
        var controller = BuildController(_ => Result<string>.NotFound("Usuario no encontrado."));

        var response = await controller.AssignTemporaryPassword(
            "no.existe", new AssignTemporaryPasswordRequest("Temp0ral!"), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AssignTemporaryPassword_usuario_de_otra_empresa_retorna_403()
    {
        var controller = BuildController(_ =>
            Result<string>.Forbidden("El usuario no pertenece a la empresa activa."));

        var response = await controller.AssignTemporaryPassword(
            "ana.perez", new AssignTemporaryPasswordRequest("Temp0ral!"), CancellationToken.None);

        response.Should().BeOfType<ObjectResult>();
        ((ObjectResult)response).StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
