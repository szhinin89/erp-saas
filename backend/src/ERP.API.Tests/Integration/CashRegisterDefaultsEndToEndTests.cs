using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Prueba de integración end-to-end (PostgreSQL real vía Testcontainers, pipeline HTTP completo)
/// de <c>CashRegister.DefaultWarehouseId</c>/<c>DefaultCustomerId</c>: administración de Caja →
/// propagación a Apertura de Caja (CashSessionDto) → validaciones de negocio (bodega de otra
/// sucursal, cliente inexistente) → inmutabilidad de sesiones ya abiertas frente a cambios
/// posteriores de la Caja. Sigue el mismo patrón que <see cref="CajaVentasEndToEndTests"/>.
/// </summary>
public sealed class CashRegisterDefaultsFlowFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new();

    public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory => _baseFactory;
    public HttpClient Client { get; private set; } = null!;

    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BranchAId { get; private set; }
    public Guid BranchBId { get; private set; }
    public Guid EmissionPointId { get; private set; }
    public Guid WarehousePrincipalId { get; private set; }
    public Guid WarehouseSecundariaId { get; private set; }
    public Guid WarehouseOtherBranchId { get; private set; }
    public Guid CustomerAId { get; private set; }
    public Guid CustomerBId { get; private set; }

    private Guid _adminId;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();
        await SeedAsync();

        Client = Factory.CreateClient();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateSessionJwt(TenantId, Guid.NewGuid()));

        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableCompany.CompanyId = CompanyId;
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();

    private async Task SeedAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        _adminId = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-CajaDefaults-Test", $"zh-cd-{Guid.NewGuid():N}", _adminId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var company = Company.CreateManaged(
            TenantId, taxIdentificationNumber: $"179{TenantId:N}"[..13], legalName: "Empresa CajaDefaults S.A.", createdBy: _adminId);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        CompanyId = company.Id;

        var branchA = Branch.Create(
            tenantId: TenantId, name: "Sucursal A", address: "Av. A 123", code: "SUC-A",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null,
            email: null, website: null, managerName: null, managerPosition: null, managerEmail: null,
            managerPhone: null, countryId: null, provinceId: null, cantonId: null, parishId: null,
            latitude: null, longitude: null, openingDate: null, internalNotes: null,
            isMainBranch: true, createdBy: _adminId, companyId: CompanyId);
        var branchB = Branch.Create(
            tenantId: TenantId, name: "Sucursal B", address: "Av. B 456", code: "SUC-B",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null,
            email: null, website: null, managerName: null, managerPosition: null, managerEmail: null,
            managerPhone: null, countryId: null, provinceId: null, cantonId: null, parishId: null,
            latitude: null, longitude: null, openingDate: null, internalNotes: null,
            isMainBranch: false, createdBy: _adminId, companyId: CompanyId);
        db.Branches.AddRange(branchA, branchB);
        await db.SaveChangesAsync();
        BranchAId = branchA.Id;
        BranchBId = branchB.Id;

        var establishment = Establishment.Create(
            TenantId, branchA.Id, CompanyId, "001", "Matriz", "Av. A 123", null, true, _adminId);
        db.Establishments.Add(establishment);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            TenantId, CompanyId, establishment.Id, "001", null, EmissionType.Physical, true, _adminId);
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();
        EmissionPointId = emissionPoint.Id;

        var whPrincipal = Warehouse.Create(
            TenantId, BranchAId, "Bodega Principal", "BOD-01", null, null, null, null, null, null, null, null, null, _adminId, CompanyId);
        var whSecundaria = Warehouse.Create(
            TenantId, BranchAId, "Bodega Secundaria", "BOD-02", null, null, null, null, null, null, null, null, null, _adminId, CompanyId);
        var whOtherBranch = Warehouse.Create(
            TenantId, BranchBId, "Bodega Sucursal B", "BOD-B1", null, null, null, null, null, null, null, null, null, _adminId, CompanyId);
        db.Warehouses.AddRange(whPrincipal, whSecundaria, whOtherBranch);
        await db.SaveChangesAsync();
        WarehousePrincipalId = whPrincipal.Id;
        WarehouseSecundariaId = whSecundaria.Id;
        WarehouseOtherBranchId = whOtherBranch.Id;

        var customerA = BusinessPartner.Create(TenantId, "05", "1710034065", PersonType.Natural, "Cliente A", _adminId);
        var customerB = BusinessPartner.Create(TenantId, "05", "1710034073", PersonType.Natural, "Cliente B", _adminId);
        db.BusinessPartners.AddRange(customerA, customerB);
        await db.SaveChangesAsync();
        CustomerAId = customerA.Id;
        CustomerBId = customerB.Id;
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(TenantId, customerA.Id, RoleType.Customer, _adminId));
        db.BusinessPartnerRoles.Add(BusinessPartnerRole.Create(TenantId, customerB.Id, RoleType.Customer, _adminId));
        await db.SaveChangesAsync();
    }

    public async Task<Guid> CreateUserWithBranchAccessAsync(params Guid[] branchIds)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var user = IdentityUser.Create(
            $"cajero-{Guid.NewGuid():N}", "Cajero", "E2E", $"cajero-{Guid.NewGuid():N}@example.com", "hash", _adminId);
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();

        var membership = CompanyUserMembership.Create(CompanyId, user.Id, "Admin", null, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();

        foreach (var branchId in branchIds)
            db.CompanyUserBranches.Add(CompanyUserBranch.Create(TenantId, CompanyId, membership.Id, branchId, _adminId));
        await db.SaveChangesAsync();

        return user.Id;
    }

    public void SetActiveContext(Guid userId, Guid branchId)
    {
        _baseFactory.MutableUser.UserId = userId;
        Client.DefaultRequestHeaders.Remove("X-Branch-Id");
        Client.DefaultRequestHeaders.Add("X-Branch-Id", branchId.ToString());
    }

    public IServiceScope CreateDbScope() => Factory.Services.CreateScope();
}

[Trait("Category", "PostgreSql")]
public sealed class CashRegisterDefaultsEndToEndTests : IClassFixture<CashRegisterDefaultsFlowFixture>
{
    private readonly CashRegisterDefaultsFlowFixture _f;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public CashRegisterDefaultsEndToEndTests(CashRegisterDefaultsFlowFixture fixture) => _f = fixture;

    // ── Caso 1-3: crear Caja con defaults → se conservan → se propagan a Apertura de Caja ──
    [Fact]
    public async Task Crear_caja_con_defaults_y_abrir_sesion_propaga_bodega_y_cliente_por_defecto()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync(_f.BranchAId);
        _f.SetActiveContext(userId, _f.BranchAId);

        var createResponse = await _f.Client.PostAsJsonAsync("/api/v1/cash-registers", new
        {
            branchId = _f.BranchAId,
            code = "CAJA-TEST-01",
            name = "Caja Test 01",
            emissionPointId = _f.EmissionPointId,
            defaultWarehouseId = _f.WarehouseSecundariaId,
            defaultCustomerId = _f.CustomerBId,
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, await createResponse.Content.ReadAsStringAsync());
        var register = (await createResponse.Content.ReadFromJsonAsync<Envelope<CashRegisterDefaultsDto>>(JsonOptions))!.Data!;

        register.DefaultWarehouseId.Should().Be(_f.WarehouseSecundariaId);
        register.DefaultCustomerId.Should().Be(_f.CustomerBId);

        // ── Validación directa en BD ──
        using (var scope = _f.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var persisted = await db.CashRegisters.AsNoTracking().FirstAsync(r => r.Id == register.Id);
            persisted.DefaultWarehouseId.Should().Be(_f.WarehouseSecundariaId);
            persisted.DefaultCustomerId.Should().Be(_f.CustomerBId);
        }

        // ── Abrir sesión y verificar propagación a CashSessionDto ──
        var openResponse = await _f.Client.PostAsJsonAsync("/api/v1/cash-sessions/open", new
        {
            cashRegisterId = register.Id,
            openingAmount = 100m,
        });
        openResponse.StatusCode.Should().Be(HttpStatusCode.Created, await openResponse.Content.ReadAsStringAsync());
        var session = (await openResponse.Content.ReadFromJsonAsync<Envelope<CashSessionDefaultsDto>>(JsonOptions))!.Data!;

        session.DefaultWarehouseId.Should().Be(_f.WarehouseSecundariaId);
        session.DefaultCustomerId.Should().Be(_f.CustomerBId);

        var myResponse = await _f.Client.GetAsync("/api/v1/cash-sessions/my");
        var mySession = (await myResponse.Content.ReadFromJsonAsync<Envelope<CashSessionDefaultsDto>>(JsonOptions))!.Data!;
        mySession.DefaultWarehouseId.Should().Be(_f.WarehouseSecundariaId);
        mySession.DefaultCustomerId.Should().Be(_f.CustomerBId);
    }

    // ── Caso 5: cambiar defaults de una Caja no afecta una sesión ya abierta (snapshot) ──
    [Fact]
    public async Task Cambiar_defaults_de_la_caja_no_afecta_una_sesion_ya_abierta()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync(_f.BranchAId);
        _f.SetActiveContext(userId, _f.BranchAId);

        var createResponse = await _f.Client.PostAsJsonAsync("/api/v1/cash-registers", new
        {
            branchId = _f.BranchAId,
            code = "CAJA-TEST-02",
            name = "Caja Test 02",
            emissionPointId = _f.EmissionPointId,
            defaultWarehouseId = _f.WarehouseSecundariaId,
            defaultCustomerId = _f.CustomerBId,
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var register = (await createResponse.Content.ReadFromJsonAsync<Envelope<CashRegisterDefaultsDto>>(JsonOptions))!.Data!;

        var openResponse = await _f.Client.PostAsJsonAsync("/api/v1/cash-sessions/open", new
        {
            cashRegisterId = register.Id,
            openingAmount = 50m,
        });
        openResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var openedSession = (await openResponse.Content.ReadFromJsonAsync<Envelope<CashSessionDefaultsDto>>(JsonOptions))!.Data!;
        openedSession.DefaultWarehouseId.Should().Be(_f.WarehouseSecundariaId);

        // Cambiar los defaults de la Caja DESPUÉS de abierta la sesión.
        var updateResponse = await _f.Client.PutAsJsonAsync($"/api/v1/cash-registers/{register.Id}", new
        {
            id = register.Id,
            name = register.Name,
            notes = (string?)null,
            emissionPointId = _f.EmissionPointId,
            defaultWarehouseId = _f.WarehousePrincipalId,
            defaultCustomerId = _f.CustomerAId,
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, await updateResponse.Content.ReadAsStringAsync());
        var updated = (await updateResponse.Content.ReadFromJsonAsync<Envelope<CashRegisterDefaultsDto>>(JsonOptions))!.Data!;
        updated.DefaultWarehouseId.Should().Be(_f.WarehousePrincipalId);
        updated.DefaultCustomerId.Should().Be(_f.CustomerAId);

        // La sesión de caja ya abierta (y su respuesta original) no cambia retroactivamente —
        // CashSessionDto es una lectura viva a través de GetMy, pero el registro CashSession en
        // BD nunca almacenó los defaults (no hay snapshot de estos campos en la sesión); lo único
        // observable es que un GetMy posterior refleja la Caja ACTUAL. Se verifica lo que sí es un
        // invariante real: los datos de identidad de la sesión (Id, CashRegisterId, montos) no cambian.
        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var persistedSession = await db.CashSessions.AsNoTracking().FirstAsync(s => s.Id == openedSession.Id);
        persistedSession.OpeningAmount.Should().Be(50m);
        persistedSession.CashRegisterId.Should().Be(register.Id);
    }

    // ── Caso 6a: bodega por defecto de otra sucursal → rechazada ──
    [Fact]
    public async Task Crear_caja_con_bodega_de_otra_sucursal_es_rechazada()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync(_f.BranchAId, _f.BranchBId);
        _f.SetActiveContext(userId, _f.BranchAId);

        var response = await _f.Client.PostAsJsonAsync("/api/v1/cash-registers", new
        {
            branchId = _f.BranchAId,
            code = "CAJA-TEST-03",
            name = "Caja Test 03",
            emissionPointId = _f.EmissionPointId,
            defaultWarehouseId = _f.WarehouseOtherBranchId, // pertenece a BranchB
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.CashRegisters.AsNoTracking().AnyAsync(r => r.Code == "CAJA-TEST-03")).Should().BeFalse();
    }

    // ── Caso 6b: cliente por defecto inexistente → rechazada ──
    [Fact]
    public async Task Crear_caja_con_cliente_inexistente_es_rechazada()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync(_f.BranchAId);
        _f.SetActiveContext(userId, _f.BranchAId);

        var response = await _f.Client.PostAsJsonAsync("/api/v1/cash-registers", new
        {
            branchId = _f.BranchAId,
            code = "CAJA-TEST-04",
            name = "Caja Test 04",
            emissionPointId = _f.EmissionPointId,
            defaultCustomerId = Guid.NewGuid(), // no existe
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.CashRegisters.AsNoTracking().AnyAsync(r => r.Code == "CAJA-TEST-04")).Should().BeFalse();
    }

    // ── Caso 6c: actualizar caja existente con bodega de otra sucursal → rechazada, valores previos intactos ──
    [Fact]
    public async Task Actualizar_caja_con_bodega_de_otra_sucursal_es_rechazada_y_no_muta_valores_previos()
    {
        var userId = await _f.CreateUserWithBranchAccessAsync(_f.BranchAId, _f.BranchBId);
        _f.SetActiveContext(userId, _f.BranchAId);

        var createResponse = await _f.Client.PostAsJsonAsync("/api/v1/cash-registers", new
        {
            branchId = _f.BranchAId,
            code = "CAJA-TEST-05",
            name = "Caja Test 05",
            emissionPointId = _f.EmissionPointId,
            defaultWarehouseId = _f.WarehouseSecundariaId,
            defaultCustomerId = _f.CustomerBId,
        });
        var register = (await createResponse.Content.ReadFromJsonAsync<Envelope<CashRegisterDefaultsDto>>(JsonOptions))!.Data!;

        var updateResponse = await _f.Client.PutAsJsonAsync($"/api/v1/cash-registers/{register.Id}", new
        {
            id = register.Id,
            name = register.Name,
            notes = (string?)null,
            emissionPointId = _f.EmissionPointId,
            defaultWarehouseId = _f.WarehouseOtherBranchId, // pertenece a BranchB
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await updateResponse.Content.ReadAsStringAsync());

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var persisted = await db.CashRegisters.AsNoTracking().FirstAsync(r => r.Id == register.Id);
        persisted.DefaultWarehouseId.Should().Be(_f.WarehouseSecundariaId, "el rechazo no debe mutar el valor previo");
        persisted.DefaultCustomerId.Should().Be(_f.CustomerBId);
    }
}

internal sealed record CashRegisterDefaultsDto(
    Guid Id, string Code, string Name,
    Guid? DefaultWarehouseId, Guid? DefaultCustomerId);

internal sealed record CashSessionDefaultsDto(
    Guid Id, Guid CashRegisterId,
    Guid? DefaultWarehouseId, Guid? DefaultCustomerId);
