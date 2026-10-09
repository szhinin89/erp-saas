using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Application.Access.Authorization;
using ERP.Domain.Access.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BranchEntity = ERP.Domain.Branches.Entities.Branch;

namespace ERP.API.Tests.Integration;

/// <summary>
/// IL-5A — PUT /api/v1/initial-load/opening-balance-date con HTTP real + PostgreSQL real (pipeline,
/// binding JSON, handler, lector y dominio reales) sobre el escenario de Sumak: Inventario Inicial
/// confirmado al 2026-09-30 y OpeningBalanceDate todavía null.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class OpeningBalanceDateHttpTests : IAsyncLifetime
{
    private static readonly DateOnly Sept30 = new(2026, 9, 30);
    private readonly PostgreSqlTestWebAppFactory _factory = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private WebApplicationFactory<Program> _app = null!;
    private Guid _tenantId;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");
        await _factory.InitializeAsync();
        await _factory.MigrateAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var tenant = Tenant.Create("IL5A-HTTP", $"il5h-{Guid.NewGuid():N}"[..16], _adminId);
        db.Tenants.Add(tenant);
        var user = IdentityUser.Create("il5ahttp", "Admin", "Sumak", $"il5a-{Guid.NewGuid():N}@test.com",
            "TEST_PASSWORD_HASH", _adminId);
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _userId = user.Id;

        _app = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IRuntimePermissionAuthorizer, AllowAllPermissionAuthorizer>()));
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        await _factory.DisposeAsync();
    }

    /// <summary>Empresa tipo Sumak: Inventario Inicial al 2026-09-30, sin fecha de apertura definida.</summary>
    private async Task<Guid> SumakAsync(bool withInitialStock, bool withRealOperation)
    {
        _factory.MutableTenant.TenantId = _tenantId;
        _factory.MutableUser.UserId = _adminId;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var company = Company.CreateManaged(_tenantId, CompanyIdentityUpdateFixture.ValidRuc(), "Sumak",
            createdBy: _adminId);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _factory.MutableCompany.CompanyId = company.Id;
        db.CompanyUserMemberships.Add(CompanyUserMembership.Create(company.Id, _userId, "Admin", null, _adminId));
        var branch = BranchEntity.Create(tenantId: _tenantId, name: "Matriz", address: "Av. 1", code: "B01",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null, email: null,
            website: null, managerName: null, managerPosition: null, managerEmail: null, managerPhone: null,
            countryId: null, provinceId: null, cantonId: null, parishId: null, latitude: null, longitude: null,
            openingDate: null, internalNotes: null, isMainBranch: true, createdBy: _adminId, companyId: company.Id);
        db.Branches.Add(branch);
        var type = ItemTypeDefinition.Create(_tenantId, "M" + company.Id.ToString("N")[..5], "Mercadería", 1, _adminId);
        db.Set<ItemTypeDefinition>().Add(type);
        await db.SaveChangesAsync();

        var item = Item.Create(_tenantId, "IL5H-" + company.Id.ToString("N")[..6], "Producto", "Producto", type.Id,
            "UNIT", ItemTaxConfig.Create(saleVatCode: "4", purchaseVatCode: "4"), ItemSaleConfig.Create(isForSale: true),
            ItemStockConfig.Create(stockControlEnabled: true), _adminId, companyId: company.Id);
        var warehouse = Warehouse.Create(_tenantId, branch.Id, "Bodega", "BOD-01", null, null, null, null, null, null,
            null, null, null, _adminId, company.Id, isMain: true);
        db.Set<Item>().Add(item);
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        StockMovement Movement(StockMovementType kind, DateOnly date, long sequence) =>
            StockMovement.Create(_tenantId, branch.Id, item.Id, warehouse.Id, kind, 10m, "UNIT", 0m, sequence, 2.5m,
                25m, date, "IL5A-HTTP", null, null, _adminId, company.Id, unitCost: 2.5m);
        if (withInitialStock)
            db.StockMovements.Add(Movement(StockMovementType.InitialBalance, Sept30, 1));
        if (withRealOperation)
            db.StockMovements.Add(Movement(StockMovementType.SaleExit, new DateOnly(2026, 10, 4), 2));
        await db.SaveChangesAsync();
        return company.Id;
    }

    private async Task<(HttpStatusCode Status, string Body)> PutAsync(Guid companyId, object body)
    {
        _factory.MutableTenant.TenantId = _tenantId;
        _factory.MutableCompany.CompanyId = companyId;
        _factory.MutableUser.UserId = _userId;
        _factory.MutableUser.Role = "Admin";
        using var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestJwtFactory.CreateSessionJwt(_tenantId, _userId, companyId, "Admin"));
        client.DefaultRequestHeaders.Add("X-Company-Id", companyId.ToString());
        var http = await client.PutAsJsonAsync("/api/v1/initial-load/opening-balance-date", body);
        return (http.StatusCode, await http.Content.ReadAsStringAsync());
    }

    private async Task<DateOnly?> StoredAsync(Guid companyId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ErpDbContext>().Companies.IgnoreQueryFilters()
            .Where(c => c.Id == companyId).Select(c => c.OpeningBalanceDate).SingleAsync();
    }

    [Fact]
    public async Task Caso1_null_con_apertura_al_2026_09_30_acepta_esa_fecha()
    {
        var company = await SumakAsync(withInitialStock: true, withRealOperation: false);

        var (status, body) = await PutAsync(company, new { openingBalanceDate = "2026-09-30" });

        status.Should().Be(HttpStatusCode.OK, body);
        JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("openingBalanceDate").GetString()
            .Should().Be("2026-09-30");
        (await StoredAsync(company)).Should().Be(Sept30);
    }

    [Fact]
    public async Task Caso2_mismo_escenario_otra_fecha_se_rechaza()
    {
        var company = await SumakAsync(withInitialStock: true, withRealOperation: false);

        var (status, body) = await PutAsync(company, new { openingBalanceDate = "2026-08-31" });

        status.Should().NotBe(HttpStatusCode.OK);
        body.Should().Contain("2026-09-30").And.Contain("2026-08-31");
        (await StoredAsync(company)).Should().BeNull();
    }

    [Fact]
    public async Task Caso3_con_operaciones_reales_la_primera_asignacion_coincidente_se_acepta()
    {
        var company = await SumakAsync(withInitialStock: true, withRealOperation: true);

        var (status, body) = await PutAsync(company, new { openingBalanceDate = "2026-09-30" });

        status.Should().Be(HttpStatusCode.OK, body);
        (await StoredAsync(company)).Should().Be(Sept30);
    }

    [Fact]
    public async Task Caso4_con_operaciones_reales_sin_apertura_confirmada_se_rechaza()
    {
        var company = await SumakAsync(withInitialStock: false, withRealOperation: true);

        var (status, body) = await PutAsync(company, new { openingBalanceDate = "2026-09-30" });

        status.Should().NotBe(HttpStatusCode.OK);
        body.Should().Contain("ninguna carga inicial");
        (await StoredAsync(company)).Should().BeNull();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"fecha\":\"2026-09-30\"}")]
    public async Task Body_sin_fecha_es_error_de_validacion_y_no_una_regla_de_dominio(string json)
    {
        var company = await SumakAsync(withInitialStock: true, withRealOperation: false);
        _factory.MutableTenant.TenantId = _tenantId;
        _factory.MutableCompany.CompanyId = company;
        _factory.MutableUser.UserId = _userId;
        using var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestJwtFactory.CreateSessionJwt(_tenantId, _userId, company, "Admin"));
        client.DefaultRequestHeaders.Add("X-Company-Id", company.ToString());

        var http = await client.PutAsync("/api/v1/initial-load/opening-balance-date",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        var body = await http.Content.ReadAsStringAsync();

        http.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, body);
        body.Should().Contain("VALIDATION_ERROR").And.Contain("openingBalanceDate").And.NotContain("DOMAIN_RULE_VIOLATION");
        (await StoredAsync(company)).Should().BeNull();
    }
}
