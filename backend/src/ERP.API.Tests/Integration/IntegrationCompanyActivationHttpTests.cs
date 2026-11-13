using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// F-02 (API-DOC-V1): <c>PUT /api/integration/v1/companies/{id}/suspend|activate</c> con HTTP real y
/// PostgreSQL real. Activate conserva <c>isActive</c>; una empresa deshabilitada no se rehabilita.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class IntegrationCompanyActivationHttpTests : IClassFixture<CompanyIdentityUpdateFixture>
{
    private readonly CompanyIdentityUpdateFixture _f;

    public IntegrationCompanyActivationHttpTests(CompanyIdentityUpdateFixture f) => _f = f;

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

    private async Task SetIsActiveAsync(Guid companyId, bool value)
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var c = await db.Companies.IgnoreQueryFilters().SingleAsync(x => x.Id == companyId);
        c.IsActive = value;
        await db.SaveChangesAsync();
    }

    private static async Task<(bool IsActive, string Operational)> PutAsync(
        HttpClient client, Guid id, string action)
    {
        var http = await client.PutAsync($"/api/integration/v1/companies/{id}/{action}", null);
        var text = await http.Content.ReadAsStringAsync();
        http.StatusCode.Should().Be(HttpStatusCode.OK, text);
        var data = JsonDocument.Parse(text).RootElement.GetProperty("data");
        return (data.GetProperty("isActive").GetBoolean(), data.GetProperty("operationalStatus").ToString());
    }

    [Fact]
    public async Task Empresa_habilitada_suspender_y_activar_conservan_isActive_true()
    {
        var id = await _f.CreateCompanyAsync(_f.TenantId);
        using var client = IntegrationClient();

        var suspended = await PutAsync(client, id, "suspend");
        var activated = await PutAsync(client, id, "activate");

        suspended.IsActive.Should().BeTrue();
        activated.IsActive.Should().BeTrue();
        (await _f.ReadAsync(id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Empresa_deshabilitada_activar_no_la_rehabilita()
    {
        var id = await _f.CreateCompanyAsync(_f.TenantId);
        await SetIsActiveAsync(id, false);
        using var client = IntegrationClient();

        await PutAsync(client, id, "suspend");
        var first = await PutAsync(client, id, "activate");
        var second = await PutAsync(client, id, "activate");

        first.IsActive.Should().BeFalse();
        second.IsActive.Should().BeFalse("activar es idempotente y no habilita administrativamente");
        var saved = await _f.ReadAsync(id);
        saved.IsActive.Should().BeFalse();
        saved.OperationalStatus.ToString().Should().Be(first.Operational);
        saved.TenantId.Should().Be(_f.TenantId);
    }

    [Fact]
    public async Task Empresa_inexistente_responde_404()
    {
        using var client = IntegrationClient();

        var http = await client.PutAsync($"/api/integration/v1/companies/{Guid.NewGuid()}/activate", null);

        http.StatusCode.Should().Be(HttpStatusCode.NotFound, await http.Content.ReadAsStringAsync());
    }
}
