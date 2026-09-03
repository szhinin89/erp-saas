using ERP.API.Tests.Support;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;

namespace ERP.API.Tests.Auth;

/// <summary>
/// AdminGlobalCore Fase C — GET /api/v1/admin-core/companies solo debe responder a un token
/// global (tenant_id == Guid.Empty + rol Admin), nunca a un Admin de tenant/empresa normal.
/// Mismo patrón que SystemProviderSettingsAuthorizationHttpTests.
/// </summary>
public sealed class AdminCoreAuthorizationHttpFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new();

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();

        Client = _baseFactory.CreateClient();
    }

    public async Task DisposeAsync() => await _baseFactory.DisposeAsync();

    public static string GlobalAdminToken() =>
        TestJwtFactory.CreateSessionJwt(Guid.Empty, Guid.NewGuid(), role: "Admin");

    public static string TenantCompanyAdminToken() =>
        TestJwtFactory.CreateSessionJwt(Guid.NewGuid(), Guid.NewGuid(), role: "Admin");
}

[Trait("Category", "PostgreSql")]
public sealed class AdminCoreAuthorizationHttpTests : IClassFixture<AdminCoreAuthorizationHttpFixture>
{
    private const string Endpoint = "/api/v1/admin-core/companies";
    private readonly AdminCoreAuthorizationHttpFixture _f;

    public AdminCoreAuthorizationHttpTests(AdminCoreAuthorizationHttpFixture fixture) => _f = fixture;

    [Fact]
    public async Task AdminGlobalCore_puede_listar()
    {
        using var response = await SendAsync(AdminCoreAuthorizationHttpFixture.GlobalAdminToken());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_normal_de_empresa_recibe_403()
    {
        using var response = await SendAsync(
            AdminCoreAuthorizationHttpFixture.TenantCompanyAdminToken()
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Usuario_sin_auth_recibe_401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        using var response = await _f.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> SendAsync(string bearerToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        return await _f.Client.SendAsync(request);
    }
}
