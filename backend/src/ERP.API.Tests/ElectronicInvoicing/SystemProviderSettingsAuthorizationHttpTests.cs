using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Configuration.Entities;
using FluentAssertions;

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

    // ZH-SRI-ANEXO26-PROVIDER-RUC-01 (ADR-038 D7, regla 2): habilitar sin fecha de vigencia se
    // rechaza en el backend (autoridad), con error estructurado asociado al campo.
    [Fact]
    public async Task Habilitar_sin_EffectiveDate_devuelve_422_estructurado_y_no_guarda()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, Endpoint)
        {
            Content = JsonContent.Create(
                new
                {
                    ruc = "1790012345001",
                    legalName = "ZH Technologies S.A.",
                    ciiuCode = "J62021002",
                    effectiveDate = (string?)null,
                    enabled = true,
                }
            ),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            SystemProviderSettingsAuthorizationHttpFixture.GlobalAdminToken()
        );

        using var response = await _f.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        doc.RootElement.GetProperty("data")
            .GetProperty("errors")
            .GetProperty("effectiveDate")[0]
            .GetString()
            .Should()
            .Be(SystemProviderSettings.EnabledWithoutEffectiveDateMessage);

        using var get = await SendAsync(
            SystemProviderSettingsAuthorizationHttpFixture.GlobalAdminToken()
        );
        using var current = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        current
            .RootElement.GetProperty("data")
            .GetProperty("enabled")
            .GetBoolean()
            .Should()
            .BeFalse();
    }

    private async Task<HttpResponseMessage> SendAsync(string bearerToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        return await _f.Client.SendAsync(request);
    }
}
