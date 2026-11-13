using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// F-09 (API-DOC-V1): <c>POST /api/integration/v1/companies</c> con HTTP real y PostgreSQL real.
/// Un RUC inválido responde 422 y no crea ninguna empresa; un RUC válido o provisional válido crea
/// la empresa con el identificador tal como se envió (sin relleno ni truncamiento).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class IntegrationCompanyTaxIdHttpTests : IClassFixture<CompanyIdentityUpdateFixture>
{
    private readonly CompanyIdentityUpdateFixture _f;

    public IntegrationCompanyTaxIdHttpTests(CompanyIdentityUpdateFixture f) => _f = f;

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

    private object Body(string taxId) =>
        new
        {
            tenantId = _f.TenantId,
            taxId,
            legalName = "Empresa Integración F09",
            mainAddress = "Av. Principal 123",
            createdByUserId = _f.UserId,
        };

    private async Task<int> CompaniesOfTenantAsync()
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await db.Companies.IgnoreQueryFilters().CountAsync(c => c.TenantId == _f.TenantId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1790012345")] // corto
    [InlineData("1710034065001999")] // largo
    [InlineData("179001234500X")] // alfanumérico
    [InlineData("1710034066001")] // 13 caracteres, dígito verificador incorrecto
    [InlineData("TMP-EC-XYZ12345")] // provisional inválido
    [InlineData(" 1710034065001")] // espacio inicial
    [InlineData("1710034065001 ")] // espacio final
    public async Task RUC_invalido_responde_422_y_no_crea_empresa(string taxId)
    {
        var before = await CompaniesOfTenantAsync();

        using var client = IntegrationClient();
        var http = await client.PostAsJsonAsync("/api/integration/v1/companies", Body(taxId));
        var text = await http.Content.ReadAsStringAsync();

        http.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, text);
        var root = JsonDocument.Parse(text).RootElement;
        root.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        root.GetProperty("data").GetProperty("errors").GetProperty("request.TaxId").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain(ERP.Application.Modules.Integration.UseCases
                .CreateIntegrationCompanyCommandValidator.InvalidTaxIdMessage);
        (await CompaniesOfTenantAsync()).Should().Be(before, "la validación ocurre antes de aprovisionar");
    }

    [Fact]
    public async Task RUC_valido_crea_la_empresa_con_el_identificador_enviado()
    {
        var ruc = CompanyIdentityUpdateFixture.ValidRuc();

        using var client = IntegrationClient();
        var http = await client.PostAsJsonAsync("/api/integration/v1/companies", Body(ruc));
        var text = await http.Content.ReadAsStringAsync();

        http.StatusCode.Should().Be(HttpStatusCode.Created, text);
        var id = JsonDocument.Parse(text).RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var saved = await _f.ReadAsync(id);
        saved.TaxIdentificationNumber.Should().Be(ruc);
        saved.IsTemporaryTaxIdentification.Should().BeFalse();
    }

    [Fact]
    public async Task Provisional_valido_crea_la_empresa_como_temporal()
    {
        const string provisional = "TMP-EC-1a2b3c4d";

        using var client = IntegrationClient();
        var http = await client.PostAsJsonAsync("/api/integration/v1/companies", Body(provisional));
        var text = await http.Content.ReadAsStringAsync();

        http.StatusCode.Should().Be(HttpStatusCode.Created, text);
        var id = JsonDocument.Parse(text).RootElement.GetProperty("data").GetProperty("id").GetGuid();
        var saved = await _f.ReadAsync(id);
        saved.TaxIdentificationNumber.Should().Be(provisional);
        saved.IsTemporaryTaxIdentification.Should().BeTrue();
    }

    [Fact]
    public async Task RUC_duplicado_valido_sigue_respondiendo_409()
    {
        var ruc = CompanyIdentityUpdateFixture.ValidRuc();
        using var client = IntegrationClient();
        (await client.PostAsJsonAsync("/api/integration/v1/companies", Body(ruc)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/integration/v1/companies", Body(ruc));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict, await second.Content.ReadAsStringAsync());
    }
}
