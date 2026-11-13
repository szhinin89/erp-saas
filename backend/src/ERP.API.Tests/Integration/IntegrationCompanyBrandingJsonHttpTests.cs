using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Integration.UseCases;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// F-01 (API-DOC-V1): <c>brandingJson</c> se conserva en v1 pero no se admite. HTTP real +
/// PostgreSQL real: un valor no vacío responde 422 sin crear empresa; nulo, vacío o solo espacios
/// no se rechazan y <c>Company.Website</c> nunca recibe branding; la validación del RUC (F-09) es
/// independiente.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class IntegrationCompanyBrandingJsonHttpTests : IClassFixture<CompanyIdentityUpdateFixture>
{
    private readonly CompanyIdentityUpdateFixture _f;

    public IntegrationCompanyBrandingJsonHttpTests(CompanyIdentityUpdateFixture f) => _f = f;

    private HttpClient IntegrationClient()
    {
        var client = _f.Client(global: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(
                Guid.Empty,
                _f.GlobalAdminId,
                role: "Admin",
                extraClaims: [new Claim("integration_api", "true")]
            )
        );
        return client;
    }

    // Se serializa el objeto tal cual: brandingJson omitido, nulo, vacío o con valor.
    private Dictionary<string, object?> Body(string taxId, bool includeBranding, string? branding = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["tenantId"] = _f.TenantId,
            ["taxId"] = taxId,
            ["legalName"] = "Empresa Branding F01",
            ["mainAddress"] = "Av. Principal 123",
            ["createdByUserId"] = _f.UserId,
        };
        if (includeBranding)
            body["brandingJson"] = branding;
        return body;
    }

    private async Task<int> CompaniesOfTenantAsync()
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await db.Companies.IgnoreQueryFilters().CountAsync(c => c.TenantId == _f.TenantId);
    }

    private static string[] ErrorKeys(JsonElement root) =>
        root.GetProperty("data").GetProperty("errors").EnumerateObject().Select(p => p.Name).Order().ToArray();

    [Theory]
    [InlineData(false, null)] // omitido
    [InlineData(true, null)] // null explícito
    [InlineData(true, "")]
    [InlineData(true, "   ")]
    public async Task BrandingJson_nulo_vacio_o_con_espacios_crea_la_empresa_sin_website(bool include, string? branding)
    {
        using var client = IntegrationClient();
        var http = await client.PostAsJsonAsync(
            "/api/integration/v1/companies",
            Body(CompanyIdentityUpdateFixture.ValidRuc(), include, branding));
        var text = await http.Content.ReadAsStringAsync();

        http.StatusCode.Should().Be(HttpStatusCode.Created, text);
        var id = JsonDocument.Parse(text).RootElement.GetProperty("data").GetProperty("id").GetGuid();
        (await _f.ReadAsync(id)).Website.Should().BeNull();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"logo\":\"x.png\",\"primary\":\"#fff\"}")]
    [InlineData("texto cualquiera")]
    public async Task BrandingJson_corto_no_vacio_responde_422_y_no_crea_empresa(string branding)
    {
        var before = await CompaniesOfTenantAsync();

        using var client = IntegrationClient();
        var http = await client.PostAsJsonAsync(
            "/api/integration/v1/companies",
            Body(CompanyIdentityUpdateFixture.ValidRuc(), true, branding));
        var text = await http.Content.ReadAsStringAsync();

        http.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, text);
        var root = JsonDocument.Parse(text).RootElement;
        root.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        ErrorKeys(root).Should().Equal("request.BrandingJson");
        root.GetProperty("data").GetProperty("errors").GetProperty("request.BrandingJson").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain(CreateIntegrationCompanyCommandValidator.UnsupportedBrandingMessage);
        (await CompaniesOfTenantAsync()).Should().Be(before);
    }

    [Fact]
    public async Task BrandingJson_largo_responde_422_y_no_400_ni_error_de_longitud_de_website()
    {
        var before = await CompaniesOfTenantAsync();
        var longJson = "{\"k\":\"" + new string('a', 300) + "\"}";

        using var client = IntegrationClient();
        var http = await client.PostAsJsonAsync(
            "/api/integration/v1/companies",
            Body(CompanyIdentityUpdateFixture.ValidRuc(), true, longJson));
        var text = await http.Content.ReadAsStringAsync();

        http.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, text);
        ErrorKeys(JsonDocument.Parse(text).RootElement).Should().Equal("request.BrandingJson");
        (await CompaniesOfTenantAsync()).Should().Be(before);
    }

    [Fact]
    public async Task RUC_invalido_con_branding_vacio_sigue_respondiendo_422_solo_por_el_RUC()
    {
        var before = await CompaniesOfTenantAsync();

        using var client = IntegrationClient();
        var http = await client.PostAsJsonAsync("/api/integration/v1/companies", Body("123", true, ""));
        var text = await http.Content.ReadAsStringAsync();

        http.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, text);
        ErrorKeys(JsonDocument.Parse(text).RootElement).Should().Equal("request.TaxId");
        (await CompaniesOfTenantAsync()).Should().Be(before);
    }

    [Fact]
    public async Task RUC_invalido_y_branding_no_vacio_informan_ambos_errores_independientes()
    {
        var before = await CompaniesOfTenantAsync();

        using var client = IntegrationClient();
        var http = await client.PostAsJsonAsync("/api/integration/v1/companies", Body("123", true, "{}"));
        var text = await http.Content.ReadAsStringAsync();

        http.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, text);
        ErrorKeys(JsonDocument.Parse(text).RootElement).Should().Equal("request.BrandingJson", "request.TaxId");
        (await CompaniesOfTenantAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Creacion_valida_conserva_201_y_el_duplicado_sigue_siendo_409()
    {
        var ruc = CompanyIdentityUpdateFixture.ValidRuc();
        using var client = IntegrationClient();

        var first = await client.PostAsJsonAsync("/api/integration/v1/companies", Body(ruc, false));
        first.StatusCode.Should().Be(HttpStatusCode.Created, await first.Content.ReadAsStringAsync());

        var second = await client.PostAsJsonAsync("/api/integration/v1/companies", Body(ruc, true, ""));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict, await second.Content.ReadAsStringAsync());
    }
}
