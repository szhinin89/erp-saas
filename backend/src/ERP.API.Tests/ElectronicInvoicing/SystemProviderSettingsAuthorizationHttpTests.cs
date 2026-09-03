using ERP.API.Tests.Support;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;

namespace ERP.API.Tests.ElectronicInvoicing;

/// <summary>
/// Cierre del hallazgo CRÍTICO de la auditoría de aislamiento (SystemProviderSettingsController
/// exigía únicamente rol Admin bajo la policy "Session", que también satisface cualquier Admin de
/// tenant/empresa normal, permitiendo leer/sobrescribir el singleton global de configuración SRI
/// del proveedor del sistema compartido por todos los tenants). El fix aplica la policy
/// "PlatformAdmin" (mismo patrón que "CompanyProvisioning" en Program.cs): exige
/// tenant_id == Guid.Empty (AdminGlobalCore real) además del rol Admin.
/// </summary>
public sealed class SystemProviderSettingsAuthorizationHttpFixture : IAsyncLifetime
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
public sealed class SystemProviderSettingsAuthorizationHttpTests
    : IClassFixture<SystemProviderSettingsAuthorizationHttpFixture>
{
    private const string Endpoint = "/api/v1/system/provider-settings";
    private readonly SystemProviderSettingsAuthorizationHttpFixture _f;

    public SystemProviderSettingsAuthorizationHttpTests(
        SystemProviderSettingsAuthorizationHttpFixture fixture
    ) => _f = fixture;

    [Fact]
    public async Task AdminGlobalCore_puede_acceder()
    {
        using var response = await SendAsync(
            SystemProviderSettingsAuthorizationHttpFixture.GlobalAdminToken()
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_normal_de_empresa_recibe_403()
    {
        using var response = await SendAsync(
            SystemProviderSettingsAuthorizationHttpFixture.TenantCompanyAdminToken()
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
