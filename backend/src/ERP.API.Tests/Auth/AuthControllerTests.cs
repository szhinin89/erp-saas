using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Auth.UseCases.PasswordReset;
using ERP.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Auth;

/// <summary>
/// Fase S1 (hardening 5A/5B). Register (POST /auth/register, anónimo, TenantId/Role del body) y
/// DirectPasswordReset (POST /auth/password-reset, anónimo, cambia password solo con
/// TenantId+Email, sin token/OTP) se eliminaron por completo — ver el comentario de clase en
/// AuthController. Estos tests prueban, por reflexión, que ambas acciones ya no existen en el
/// tipo (falla si alguien las reintroduce), y que el flujo oficial de reset por token
/// (ForgotPassword + ResetPasswordWithToken) sigue enrutando correctamente sin cambios.
/// </summary>
public sealed class AuthControllerTests
{
    private static AuthController BuildController(Func<object, object> handler)
    {
        var controller = new AuthController(new StubMediator(handler));
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

    // ── 5A / 5B: endpoints eliminados — no deben poder reintroducirse sin que este test falle ──

    [Fact]
    public void AuthController_ya_no_expone_una_accion_Register()
    {
        typeof(AuthController)
            .GetMethod("Register")
            .Should()
            .BeNull(
                "POST /auth/register permitía crear un usuario Admin en cualquier tenant existente sin autenticación (hallazgo 5A)"
            );
    }

    [Fact]
    public void AuthController_ya_no_expone_una_accion_DirectPasswordReset()
    {
        typeof(AuthController)
            .GetMethod("DirectPasswordReset")
            .Should()
            .BeNull(
                "POST /auth/password-reset cambiaba la contraseña de cualquier usuario solo con TenantId+Email, sin token ni OTP (hallazgo 5B)"
            );
    }

    // ── El flujo oficial de reset por token sigue funcionando sin cambios ──

    [Fact]
    public async Task ForgotPassword_sigue_enrutando_correctamente_al_UseCase_oficial()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<bool>.Success(true);
        });

        var response = await controller.ForgotPassword(
            new ForgotPasswordCommand("ana@test.com"),
            CancellationToken.None
        );

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(new ForgotPasswordCommand("ana@test.com"));
    }

    [Fact]
    public async Task ResetPasswordWithToken_sigue_enrutando_correctamente_al_UseCase_oficial()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<bool>.Success(true);
        });

        var command = new ResetPasswordWithTokenCommand("raw-token", "NewS3cure!", null);
        var response = await controller.ResetPasswordWithToken(command, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        sentRequest.Should().Be(command);
    }
}
