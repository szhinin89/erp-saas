using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Application.Access.Authorization;
using ERP.Domain.Access.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// ZH-API-RESULT-STATUS-MAPPING-01 — contrato HTTP real (handlers reales, PostgreSQL) de los
/// endpoints cuyo status depende de ToOkOrNotFound / ToFileOrNotFound:
///   - los que cambian: logo/content y logo-alt/content sin empresa operativa (404 → 403 FORBIDDEN);
///   - los que NO deben cambiar: recurso inexistente → 404, logo inexistente → 404, y el 404
///     intencional de GetCompanyById sobre una empresa ajena (no revela su existencia).
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ResultStatusMappingHttpTests : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _factory = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _foreignCompanyId;
    private Guid _userId;
    private WebApplicationFactory<Program> _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");
        await _factory.InitializeAsync();
        await _factory.MigrateAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var tenant = Tenant.Create("ZH-Status", $"zh-{Guid.NewGuid():N}", _adminId);
            var foreignTenant = Tenant.Create("ZH-Otro", $"zo-{Guid.NewGuid():N}", _adminId);
            db.Tenants.AddRange(tenant, foreignTenant);
            await db.SaveChangesAsync();
            _tenantId = tenant.Id;

            var company = Company.CreateManaged(tenant.Id, $"179{tenant.Id:N}"[..13], "Empresa Test", createdBy: _adminId);
            var foreign = Company.CreateManaged(foreignTenant.Id, $"179{foreignTenant.Id:N}"[..13], "Empresa Ajena", createdBy: _adminId);
            db.Companies.AddRange(company, foreign);
            await db.SaveChangesAsync();
            _companyId = company.Id;
            _foreignCompanyId = foreign.Id;

            var user = IdentityUser.Create("sadmi", "Admin", "Test", $"admin-{Guid.NewGuid():N}@test.com", "TEST_PASSWORD_HASH", _adminId);
            db.IdentityUsers.Add(user);
            await db.SaveChangesAsync();
            db.CompanyUserMemberships.Add(CompanyUserMembership.Create(_companyId, user.Id, "Admin", null, _adminId));
            await db.SaveChangesAsync();
            _userId = user.Id;
        }

        _app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IRuntimePermissionAuthorizer, AllowAllPermissionAuthorizer>()));
        _client = _app.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(_tenantId, _userId, _companyId, "Admin")
        );
        _factory.MutableTenant.TenantId = _tenantId;
        _factory.MutableCompany.CompanyId = _companyId;
        _factory.MutableUser.UserId = _userId;
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        await _factory.DisposeAsync();
    }

    private async Task<(HttpStatusCode Status, string? Code, string[] Errors)> Get(string url)
    {
        var response = await _client.GetAsync(url);
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('{'))
            return (response.StatusCode, null, []);
        var json = JsonDocument.Parse(text).RootElement;
        var errors = json.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array
            ? e.EnumerateArray().Select(x => x.GetString()!).ToArray()
            : [];
        return (response.StatusCode, json.GetProperty("code").GetString(), errors);
    }

    // ── Cambian ──────────────────────────────────────────────────────────────

    /// <summary>
    /// ResolveItemQuery devuelve VALIDATION_ERROR para un código en blanco (antes ToOkOrNotFound lo
    /// habría convertido en 404; ahora sería 422, cubierto en ResultStatusMappingTests), pero por HTTP
    /// es inalcanzable: [ApiController] rechaza el {code} en blanco en el model binding con 400
    /// ProblemDetails antes de llegar a Application. Contrato real, sin cambios.
    /// </summary>
    [Fact]
    public async Task Items_resolve_con_codigo_en_blanco_lo_rechaza_el_model_binding_con_400()
    {
        var response = await _client.GetAsync("/api/v1/items/resolve/%20");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"errors\"").And.Contain("code");
    }

    [Theory]
    [InlineData("/api/v1/companies/profile/logo/content")]
    [InlineData("/api/v1/companies/profile/logo-alt/content")]
    public async Task Logo_sin_empresa_operativa_responde_403_COMPANY_SCOPE_FORBIDDEN(string url)
    {
        _factory.MutableCompany.CompanyId = Guid.Empty;

        var (status, code, errors) = await Get(url);

        // ZH-SCOPE-ERROR-SEMANTICS-01: el handler propaga el código del guard — mismo 403 y mismo
        // COMPANY_SCOPE_FORBIDDEN que CompanyScopeBehavior (antes FORBIDDEN genérico).
        status.Should().Be(HttpStatusCode.Forbidden);
        code.Should().Be("COMPANY_SCOPE_FORBIDDEN");
        errors.Should().Equal("No hay empresa operativa seleccionada.");
    }

    // ── No cambian ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/v1/companies/profile/logo/content", "La empresa no tiene un logo configurado.")]
    [InlineData("/api/v1/companies/profile/logo-alt/content", null)]
    public async Task Logo_inexistente_sigue_404_NOT_FOUND(string url, string? message)
    {
        var (status, code, errors) = await Get(url);

        status.Should().Be(HttpStatusCode.NotFound);
        code.Should().Be("NOT_FOUND");
        errors.Should().ContainSingle();
        if (message is not null)
            errors.Should().Equal(message);
    }

    [Fact]
    public async Task Items_resolve_inexistente_sigue_200_con_data_null()
    {
        var response = await _client.GetAsync("/api/v1/items/resolve/NO-EXISTE-123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement
            .GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("/api/v1/items/{0}")]
    [InlineData("/api/v1/inventory/warehouses/{0}")]
    [InlineData("/api/v1/settings/branches/{0}")]
    public async Task Recurso_inexistente_sigue_404_NOT_FOUND(string template)
    {
        var (status, code, errs) = await Get(string.Format(template, Guid.NewGuid()));

        status.Should().Be(HttpStatusCode.NotFound, $"{code}: {string.Join(" | ", errs)}");
        code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task GetCompanyById_de_una_empresa_ajena_sigue_404_intencional_sin_revelar_existencia()
    {
        var foreign = await Get($"/api/v1/companies/{_foreignCompanyId}");
        var missing = await Get($"/api/v1/companies/{Guid.NewGuid()}");

        foreign.Status.Should().Be(HttpStatusCode.NotFound);
        foreign.Code.Should().Be("NOT_FOUND");
        foreign.Should().BeEquivalentTo(missing, "una empresa ajena es indistinguible de una inexistente");
    }
}
