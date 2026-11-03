using System.Net;
using System.Text.Json;
using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.GlobalLogin;
using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Auth;

/// <summary>
/// SEC-A01 — contrato HTTP de la admisión de <c>login</c> y <c>global-login</c>: límite excedido →
/// 429 <c>RATE_LIMITED</c> y Redis de límites no disponible → 503 <c>LOGIN_THROTTLE_UNAVAILABLE</c>,
/// en ambos casos SIN ejecutar el handler (no se verifica la contraseña). Solo un login exitoso
/// devuelve el cupo.
/// </summary>
public sealed class AuthLoginThrottleContractTests
{
    private static readonly AuthResponseDto Session = new(
        Guid.NewGuid(),
        "Ana",
        "ana",
        "ana@test.com",
        "Admin",
        Guid.NewGuid(),
        "access-token"
    );

    public static TheoryData<string> Endpoints => new() { "login", "global-login" };

    private static async Task<(IActionResult Response, int HandlerCalls)> Invoke(
        string endpoint,
        StubLoginAttemptThrottle throttle,
        Result<AuthResponseDto> handlerResult
    )
    {
        var handlerCalls = 0;
        var controller = new AuthController(
            new StubMediator(_ =>
            {
                handlerCalls++;
                return handlerResult;
            })
        );
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment());
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        http.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.7");
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        var response = endpoint == "login"
            ? await controller.Login(new LoginCommand("Cajero01", "pw"), throttle, default)
            : await controller.GlobalLogin(
                new GlobalLoginCommand("Cajero01", "pw"),
                throttle,
                default
            );
        return (response, handlerCalls);
    }

    private static (int Status, string? Code, string[] Errors) Read(IActionResult result)
    {
        var obj = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var json = JsonSerializer.SerializeToElement(obj.Value);
        var errors =
            json.TryGetProperty("Data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("errors", out var e)
                ? e.EnumerateArray().Select(x => x.GetString()!).ToArray()
                : [];
        return (obj.StatusCode ?? 200, json.GetProperty("Code").GetString(), errors);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Limite_excedido_responde_429_RATE_LIMITED_sin_ejecutar_el_handler(
        string endpoint
    )
    {
        var throttle = new StubLoginAttemptThrottle(LoginAdmissionStatus.RateLimited);

        var (response, handlerCalls) = await Invoke(
            endpoint,
            throttle,
            Result<AuthResponseDto>.Success(Session)
        );

        Read(response).Status.Should().Be(429);
        Read(response).Code.Should().Be(ApiResponseCodes.Common.RateLimited);
        handlerCalls.Should().Be(0);
        throttle.Succeeded.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Redis_de_limites_no_disponible_responde_503_sin_detalle_y_sin_ejecutar_el_handler(
        string endpoint
    )
    {
        var throttle = new StubLoginAttemptThrottle(LoginAdmissionStatus.Unavailable);

        var (response, handlerCalls) = await Invoke(
            endpoint,
            throttle,
            Result<AuthResponseDto>.Success(Session)
        );

        Read(response)
            .Should()
            .Be((503, ApiResponseCodes.Auth.LoginThrottleUnavailable, Array.Empty<string>()));
        handlerCalls.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Login_exitoso_devuelve_el_cupo_y_el_fallido_lo_conserva(string endpoint)
    {
        var ok = StubLoginAttemptThrottle.AdmitAll;
        var failed = StubLoginAttemptThrottle.AdmitAll;

        var (okResponse, _) = await Invoke(endpoint, ok, Result<AuthResponseDto>.Success(Session));
        var (failedResponse, _) = await Invoke(
            endpoint,
            failed,
            Result<AuthResponseDto>.Failure("Credenciales inválidas.")
        );

        Read(okResponse).Status.Should().Be(200);
        ok.Succeeded.Should().Be(1);
        Read(failedResponse).Status.Should().Be(401);
        failed.Succeeded.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task La_admision_recibe_el_usuario_del_command_y_la_IP_de_la_conexion(
        string endpoint
    )
    {
        var throttle = StubLoginAttemptThrottle.AdmitAll;

        await Invoke(endpoint, throttle, Result<AuthResponseDto>.Success(Session));

        throttle.Begun.Should().Be(1);
        throttle.LastIdentifier.Should().Be("Cajero01");
        throttle.LastClientIp.Should().Be("198.51.100.7");
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "ERP.API.Tests";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } =
            null!;
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            null!;
    }
}
