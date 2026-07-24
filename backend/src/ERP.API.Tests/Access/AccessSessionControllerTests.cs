using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Auth.UseCases.ChangeMyPassword;
using ERP.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Access;

/// <summary>
/// Fase E: contrato de AccessSessionController.ChangeMyPassword con StubMediator — mapeo de
/// Request a Command y policy declarada (solo "Session", sin permiso adicional, porque opera sobre
/// uno mismo). No prueba de nuevo la imposición real de autenticación, ya cubierta por el pipeline
/// de ASP.NET Core.
/// </summary>
public sealed class AccessSessionControllerTests
{
    private static AccessSessionController BuildController(Func<object, object> handler)
    {
        var controller = new AccessSessionController(new StubMediator(handler));
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
    public void ChangeMyPassword_exige_policy_Session()
    {
        var method = typeof(AccessSessionController).GetMethod(nameof(AccessSessionController.ChangeMyPassword))!;
        var attr = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attr.Policy.Should().Be("Session");
    }

    [Fact]
    public async Task ChangeMyPassword_exitoso_retorna_200_y_envia_el_command_sin_UserId_en_el_contrato()
    {
        object? sentRequest = null;
        var controller = BuildController(req =>
        {
            sentRequest = req;
            return Result<bool>.Success(true);
        });

        var response = await controller.ChangeMyPassword(
            new ChangeMyPasswordRequest("old-pass", "N3wPassword!"), CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        var sentCommand = sentRequest.Should().BeOfType<ChangeMyPasswordCommand>().Subject;
        sentCommand.CurrentPassword.Should().Be("old-pass");
        sentCommand.NewPassword.Should().Be("N3wPassword!");
    }

    [Fact]
    public async Task ChangeMyPassword_con_contrasena_actual_incorrecta_retorna_400()
    {
        var controller = BuildController(_ => Result<bool>.Failure("La contraseña actual no es correcta."));

        var response = await controller.ChangeMyPassword(
            new ChangeMyPasswordRequest("wrong", "N3wPassword!"), CancellationToken.None);

        response.Should().BeOfType<BadRequestObjectResult>();
    }
}
