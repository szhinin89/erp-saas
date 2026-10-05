using ERP.API.Contracts;
using ERP.API.Controllers;
using ERP.API.Tests.Support;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.CompletePasswordReset;
using ERP.Application.Auth.UseCases.GlobalLogin;
using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Auth.UseCases.PasswordReset;
using ERP.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace ERP.API.Tests.Auth;

/// <summary>
/// ZH-API-THIN-AUTH-01 — contrato de la cookie httpOnly de refresh (<c>erp_refresh_token</c>) en
/// todos los endpoints que reciben un <see cref="AuthResponseDto"/>, fijado ANTES de unificar la
/// emisión en AuthRefreshCookieHelper. Se lee el header Set-Cookie real que produce ASP.NET Core:
///   - éxito con RefreshToken + RefreshTokenExpiry → una cookie: HttpOnly, SameSite=Strict,
///     Path=/api, Expires = RefreshTokenExpiry, Secure solo si el request es HTTPS;
///   - éxito sin RefreshToken (o sin expiración, o sin valor) → ninguna cookie;
///   - fallo → ninguna cookie (y el mismo status que antes);
///   - endpoints sin sesión (forgot/reset password, my-companies) → nunca tocan la cookie;
///   - logout → borra /api y el path legacy /api/auth, aunque el comando falle.
/// </summary>
public sealed class AuthRefreshCookieContractTests
{
    private const string CookieName = "erp_refresh_token";
    private static readonly DateTime Expiry = new(2026, 10, 1, 12, 30, 45, DateTimeKind.Utc);

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

    private static T WithContext<T>(T controller, bool https, string? requestCookie = null)
        where T : ControllerBase
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment());
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        http.Request.Scheme = https ? "https" : "http";
        if (requestCookie is not null)
            http.Request.Headers.Append("Cookie", $"{CookieName}={requestCookie}");
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static AuthResponseDto Response(string? refreshToken, DateTime? expiry) =>
        new(Guid.NewGuid(), "Ana", "ana", "ana@test.com", "Admin", Guid.NewGuid(), "access-token")
        {
            RefreshToken = refreshToken,
            RefreshTokenExpiry = expiry,
        };

    private static IList<SetCookieHeaderValue> SetCookies(ControllerBase controller) =>
        SetCookieHeaderValue.ParseList(controller.HttpContext.Response.Headers.SetCookie.ToArray());

    // ── Endpoints que reciben AuthResponseDto ────────────────────────────────

    public static TheoryData<string> AuthResponseEndpoints =>
        new()
        {
            "login",
            "global-login",
            "complete-password-reset",
            "refresh",
            "reauthenticate",
            "switch-company",
            "global/operate-company",
            "global/return",
        };

    /// <summary>Invoca el endpoint con un mediator que responde <paramref name="result"/>.</summary>
    private static async Task<(IActionResult Response, ControllerBase Controller)> Invoke(
        string endpoint,
        Result<AuthResponseDto> result,
        bool https
    )
    {
        var mediator = new StubMediator(_ => result);
        switch (endpoint)
        {
            case "login":
                {
                    var c = WithContext(new AuthController(mediator), https);
                    return (await c.Login(new LoginCommand("ana", "pw"), default), c);
                }
            case "global-login":
                {
                    var c = WithContext(new AuthController(mediator), https);
                    return (await c.GlobalLogin(new GlobalLoginCommand("ana", "pw"), default), c);
                }
            case "complete-password-reset":
                {
                    var c = WithContext(new AuthController(mediator), https);
                    return (
                        await c.CompletePasswordReset(
                            new CompletePasswordResetCommand("tok", "N3wPass!"),
                            default
                        ),
                        c
                    );
                }
            case "refresh":
                {
                    var c = WithContext(new AuthController(mediator), https);
                    return (await c.Refresh(new RefreshRequest("raw-token"), default), c);
                }
            case "reauthenticate":
                {
                    var c = WithContext(
                        new AuthController(mediator),
                        https,
                        requestCookie: "raw-token"
                    );
                    return (await c.Reauthenticate(new ReauthenticateRequest("pw"), default), c);
                }
            case "switch-company":
                {
                    var c = WithContext(new AuthController(mediator), https);
                    return (
                        await c.SwitchCompany(new SwitchCompanyRequest(Guid.NewGuid()), default),
                        c
                    );
                }
            case "global/operate-company":
                {
                    var c = WithContext(new GlobalAuthController(mediator), https);
                    return (
                        await c.OperateCompany(new OperateCompanyRequest(Guid.NewGuid()), default),
                        c
                    );
                }
            case "global/return":
                {
                    var c = WithContext(new GlobalAuthController(mediator), https);
                    return (await c.Return(default), c);
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(endpoint));
        }
    }

    [Theory]
    [MemberData(nameof(AuthResponseEndpoints))]
    public async Task Exito_con_refresh_token_emite_una_cookie_con_la_politica_vigente(
        string endpoint
    )
    {
        foreach (var https in new[] { true, false })
        {
            var (response, controller) = await Invoke(
                endpoint,
                Result<AuthResponseDto>.Success(Response("new-refresh", Expiry)),
                https
            );

            response.Should().BeOfType<OkObjectResult>();
            var cookie = SetCookies(controller)
                .Should()
                .ContainSingle($"{endpoint} emite exactamente una cookie")
                .Subject;
            cookie.Name.Value.Should().Be(CookieName);
            cookie.Value.Value.Should().Be("new-refresh");
            cookie.HttpOnly.Should().BeTrue();
            cookie.SameSite.Should().Be(Microsoft.Net.Http.Headers.SameSiteMode.Strict);
            cookie.Path.Value.Should().Be("/api");
            cookie.Expires.Should().Be(new DateTimeOffset(Expiry));
            cookie.MaxAge.Should().BeNull("la política usa Expires, no Max-Age");
            cookie.Secure.Should().Be(https, "Secure sigue a Request.IsHttps");
            cookie.Domain.HasValue.Should().BeFalse();
        }
    }

    [Theory]
    [MemberData(nameof(AuthResponseEndpoints))]
    public async Task Exito_sin_refresh_token_o_sin_expiracion_no_emite_cookie(string endpoint)
    {
        foreach (
            var value in new[]
            {
                Response(null, Expiry),
                Response("token-sin-expiracion", null),
                null,
            }
        )
        {
            var (response, controller) = await Invoke(
                endpoint,
                Result<AuthResponseDto>.Success(value!),
                https: true
            );

            response.Should().BeOfType<OkObjectResult>();
            controller.HttpContext.Response.Headers.SetCookie.Should().BeEmpty(endpoint);
        }
    }

    [Theory]
    [InlineData("login", typeof(UnauthorizedObjectResult))]
    [InlineData("global-login", typeof(UnauthorizedObjectResult))]
    [InlineData("complete-password-reset", typeof(UnauthorizedObjectResult))]
    [InlineData("refresh", typeof(UnauthorizedObjectResult))]
    [InlineData("reauthenticate", typeof(UnauthorizedObjectResult))]
    [InlineData("switch-company", typeof(BadRequestObjectResult))]
    [InlineData("global/operate-company", typeof(BadRequestObjectResult))]
    [InlineData("global/return", typeof(BadRequestObjectResult))]
    public async Task Fallo_no_emite_cookie_y_conserva_el_status(string endpoint, Type expected)
    {
        var (response, controller) = await Invoke(
            endpoint,
            Result<AuthResponseDto>.Failure("No autorizado."),
            https: true
        );

        response.Should().BeOfType(expected);
        controller.HttpContext.Response.Headers.SetCookie.Should().BeEmpty(endpoint);
    }

    [Fact]
    public async Task Refresh_rate_limited_responde_429_sin_cookie()
    {
        var (response, controller) = await Invoke(
            "refresh",
            Result<AuthResponseDto>.Failure(
                "Demasiados intentos.",
                ApiResponseCodes.Common.RateLimited
            ),
            https: true
        );

        response
            .Should()
            .BeOfType<ObjectResult>()
            .Which.StatusCode.Should()
            .Be(StatusCodes.Status429TooManyRequests);
        controller.HttpContext.Response.Headers.SetCookie.Should().BeEmpty();
    }

    [Fact]
    public async Task Refresh_y_Reauthenticate_sin_token_responden_401_sin_llamar_al_mediator_ni_emitir_cookie()
    {
        var called = false;
        var mediator = new StubMediator(_ =>
        {
            called = true;
            return Result<AuthResponseDto>.Success(Response("x", Expiry));
        });
        var refresh = WithContext(new AuthController(mediator), https: true);
        var reauth = WithContext(new AuthController(mediator), https: true);

        (await refresh.Refresh(new RefreshRequest(), default))
            .Should()
            .BeOfType<UnauthorizedObjectResult>();
        (await reauth.Reauthenticate(new ReauthenticateRequest("pw"), default))
            .Should()
            .BeOfType<UnauthorizedObjectResult>();

        called.Should().BeFalse();
        refresh.HttpContext.Response.Headers.SetCookie.Should().BeEmpty();
        reauth.HttpContext.Response.Headers.SetCookie.Should().BeEmpty();
    }

    // ── Endpoints que no deben tocar la cookie ───────────────────────────────

    [Fact]
    public async Task Endpoints_sin_sesion_nunca_tocan_la_cookie()
    {
        var mediator = new StubMediator(request =>
            request switch
            {
                ForgotPasswordCommand => Result<bool>.Success(true),
                ResetPasswordWithTokenCommand => Result<bool>.Success(true),
                _ => Result<IReadOnlyList<AccessibleCompanyDto>>.Success(
                    Array.Empty<AccessibleCompanyDto>()
                ),
            }
        );
        var forgot = WithContext(
            new AuthController(mediator),
            https: true,
            requestCookie: "existing"
        );
        var reset = WithContext(
            new AuthController(mediator),
            https: true,
            requestCookie: "existing"
        );
        var myCompanies = WithContext(
            new AuthController(mediator),
            https: true,
            requestCookie: "existing"
        );

        await forgot.ForgotPassword(new ForgotPasswordCommand("ana@test.com"), default);
        await reset.ResetPasswordWithToken(
            new ResetPasswordWithTokenCommand("tok", "N3wPass!", null),
            default
        );
        await myCompanies.ListMyCompanies(default);

        forgot.HttpContext.Response.Headers.SetCookie.Should().BeEmpty();
        reset.HttpContext.Response.Headers.SetCookie.Should().BeEmpty();
        myCompanies.HttpContext.Response.Headers.SetCookie.Should().BeEmpty();
    }

    // ── Logout ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Logout_borra_la_cookie_en_path_vigente_y_legacy_aunque_el_comando_falle(
        bool https,
        bool commandSucceeds
    )
    {
        var mediator = new StubMediator(_ =>
            commandSucceeds ? Result<string>.Success("ok") : Result<string>.Failure("inválido")
        );
        var controller = WithContext(
            new AuthController(mediator),
            https,
            requestCookie: "raw-token"
        );

        await controller.Logout(new LogoutRequest(null), default);

        var cookies = SetCookies(controller);
        cookies.Select(c => c.Path.Value).Should().BeEquivalentTo(["/api", "/api/auth"]);
        foreach (var cookie in cookies)
        {
            cookie.Name.Value.Should().Be(CookieName);
            cookie.Value.Value.Should().BeEmpty();
            cookie.Expires.Should().Be(DateTimeOffset.UnixEpoch, "Delete expira la cookie en 1970");
            cookie.HttpOnly.Should().BeTrue();
            cookie.SameSite.Should().Be(Microsoft.Net.Http.Headers.SameSiteMode.Strict);
            cookie.Secure.Should().Be(https);
        }
    }
}
