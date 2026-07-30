using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.API.Tests.Integration;

public class BusinessPartnerValidationTests : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _factory = new();

    private HttpClient _client = null!;

    private Guid _tenantId;
    private Guid _companyId;
    private readonly Guid _adminId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable(
            "JWT__SECRETKEY",
            IntegrationTestConstants.JwtSecretKey);

        Environment.SetEnvironmentVariable(
            "JWT__ISSUER",
            "ZHTechnologies");

        Environment.SetEnvironmentVariable(
            "JWT__AUDIENCE",
            "ERPUsers");

        await _factory.InitializeAsync();
        await _factory.MigrateAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

            var tenant = Tenant.Create(
                "ZH-Test",
                $"zh-{Guid.NewGuid():N}",
                _adminId);

            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            _tenantId = tenant.Id;

            var company = Company.CreateManaged(
                tenant.Id,
                $"179{tenant.Id:N}"[..13],
                "Empresa Test",
                createdBy: _adminId
            );

            db.Companies.Add(company);
            await db.SaveChangesAsync();

            _companyId = company.Id;
            var user = IdentityUser.Create(
                 "sadmi",
                 "Admin",
                 "Test",
                 $"admin-{Guid.NewGuid():N}@test.com",
                 "TEST_PASSWORD_HASH",
                 _adminId
             );

            db.IdentityUsers.Add(user);
            await db.SaveChangesAsync();

            var membership = CompanyUserMembership.Create(
                _companyId,
                user.Id,
                "Admin",
                null,
                _adminId
            );

            db.CompanyUserMemberships.Add(membership);
            await db.SaveChangesAsync();
        }

        _client = _factory.CreateClient();

        _client.DefaultRequestHeaders.Authorization =
     new AuthenticationHeaderValue(
         "Bearer",
         TestJwtFactory.CreateSessionJwt(
             _tenantId,
             _adminId,
             _companyId,
             "Admin"));

        _factory.MutableTenant.TenantId = _tenantId;
        _factory.MutableCompany.CompanyId = _companyId;
        _factory.MutableUser.UserId = _adminId;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Debe_rechazar_RUC_sociedad_privada_invalido()
    {
        var request = new
        {
            identificationType = "04",
            identificationNumber = "0302126842001",
            legalEntityTypeCode = 2,
            legalName = "Empresa Prueba",
            tradeName = "Empresa Prueba",
            countryCode = "EC"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/v1/master/business-partners",
            request);

        response.StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Debe_permitir_RUC_sociedad_privada_valido()
    {
        var request = new
        {
            identificationType = "04",
            identificationNumber = "1791352688001",
            legalEntityTypeCode = 2,
            legalName = "QUALA ECUADOR S A",
            tradeName = "QUALA ECUADOR",
            countryCode = "EC"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/v1/master/business-partners",
            request);

        response.StatusCode.Should()
            .Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Debe_permitir_RUC_persona_natural_valido()
    {
        var request = new
        {
            identificationType = "04",
            identificationNumber = "0302126842001",
            legalEntityTypeCode = 1,
            legalName = "Sebastian Zhinin",
            tradeName = "Sebastian",
            countryCode = "EC"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/v1/master/business-partners",
            request);

        response.StatusCode.Should()
            .Be(HttpStatusCode.Created);
    }

    // ── Inferencia automática de LegalEntityType ─────────────────────────────

    [Fact]
    public async Task Debe_inferir_LegalEntityType_de_RUC_sin_enviarlo()
    {
        var request = new
        {
            identificationType = "04",
            identificationNumber = "1791352688001", // Sociedad Privada (3.er dígito 9)
            legalName = "QUALA ECUADOR S A",
            tradeName = "QUALA ECUADOR",
            countryCode = "EC"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/v1/master/business-partners",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetProperty("legalEntityTypeCode").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Debe_inferir_LegalEntityType_de_CI_sin_enviarlo()
    {
        var request = new
        {
            identificationType = "05",
            identificationNumber = "0302126842",
            legalName = "Sebastian Zhinin",
            countryCode = "EC"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/v1/master/business-partners",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetProperty("legalEntityTypeCode").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Debe_rechazar_LegalEntityType_explicito_que_contradice_al_RUC()
    {
        var request = new
        {
            identificationType = "04",
            identificationNumber = "1791352688001", // infiere Sociedad Privada (2)
            legalEntityTypeCode = 1, // contradice
            legalName = "QUALA ECUADOR S A",
            countryCode = "EC"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/v1/master/business-partners",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Debe_rechazar_Pasaporte_sin_LegalEntityType_explicito()
    {
        // No puede inferirse — nunca se asume Persona Natural por defecto.
        var request = new
        {
            identificationType = "06",
            identificationNumber = "P12345678",
            legalName = "Extranjero SA",
            countryCode = "US"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/v1/master/business-partners",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Debe_permitir_Pasaporte_con_LegalEntityType_explicito()
    {
        var request = new
        {
            identificationType = "06",
            identificationNumber = "P12345678",
            legalEntityTypeCode = 2,
            legalName = "Extranjero SA",
            countryCode = "US"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/v1/master/business-partners",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Debe_rechazar_LegalEntityType_inexistente_en_catalogo()
    {
        // 99 no existe en LegalEntityTypeCatalog (solo 1/2/3). No inferible (Pasaporte)
        // obliga a enviar un valor explícito — debe validarse contra el catálogo activo.
        var request = new
        {
            identificationType = "06",
            identificationNumber = "P12345678",
            legalEntityTypeCode = 99,
            legalName = "Extranjero SA",
            countryCode = "US"
        };

        var response = await _client.PostAsJsonAsync(
            "/api/v1/master/business-partners",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── UpdateProfile: consistencia con la identificación ────────────────────

    [Fact]
    public async Task UpdateProfile_debe_rechazar_LegalEntityType_que_contradiga_RUC_existente()
    {
        var createRequest = new
        {
            identificationType = "04",
            identificationNumber = "1791352688001", // Sociedad Privada
            legalName = "QUALA ECUADOR S A",
            countryCode = "EC"
        };
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/master/business-partners",
            createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("data").GetProperty("id").GetString();

        var updateRequest = new
        {
            legalName = "QUALA ECUADOR S A",
            legalEntityTypeCode = 1, // contradice el RUC (2)
            countryCode = "EC"
        };
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/master/business-partners/{id}",
            updateRequest);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── UpdateIdentification: recalcula LegalEntityType automáticamente ─────

    [Fact]
    public async Task UpdateIdentification_a_RUC_debe_recalcular_LegalEntityType()
    {
        var createRequest = new
        {
            identificationType = "06",
            identificationNumber = "P12345678",
            legalEntityTypeCode = 1, // valor manual inicial, sin relación con el RUC nuevo
            legalName = "Persona X",
            countryCode = "US"
        };
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/master/business-partners",
            createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("data").GetProperty("id").GetString();

        var identificationRequest = new
        {
            identificationType = "04",
            identificationNumber = "1791352688001", // Sociedad Privada
        };
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/master/business-partners/{id}/identification",
            identificationRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetProperty("legalEntityTypeCode").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Identificacion_duplicada_devuelve_409()
    {
        var request = new
        {
            identificationType = "04",
            identificationNumber = "1791352688001",
            legalName = "QUALA ECUADOR S A",
            countryCode = "EC"
        };
        var first = await _client.PostAsJsonAsync("/api/v1/master/business-partners", request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync("/api/v1/master/business-partners", request);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
