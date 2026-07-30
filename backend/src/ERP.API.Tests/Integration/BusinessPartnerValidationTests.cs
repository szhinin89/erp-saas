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
            .Be(HttpStatusCode.OK);
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
            .Be(HttpStatusCode.OK);
    }
}
