using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// BRANCH-ADMIN-CATALOG-ACCESS-REVIEW-01 (opción A) — fija contra PostgreSQL real que el catálogo
/// de sucursales y el alta/edición de bodegas son administración company-wide autorizada por
/// <c>settings.branches.*</c> / <c>inventory.warehouses.*</c>, no por la asignación de sucursales
/// del usuario; deshabilitar bodega es branch-scoped; otra empresa sigue invisible (404) y sin el
/// permiso de escritura se responde 403.
/// </summary>
public sealed class BranchWarehouseCatalogAccessFixture : IAsyncLifetime
{
    public const string Role = "Cajero";

    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);
    private Guid _adminId;

    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid BranchA1Id { get; private set; }
    public Guid BranchA2Id { get; private set; }
    public Guid ForeignBranchId { get; private set; }
    public Guid WarehouseA2Id { get; private set; }
    public Guid WarehouseA2ForDisableId { get; private set; }
    public Guid WarehouseBId { get; private set; }

    /// <summary>Todos los usuarios tienen CompanyUserBranch solo en A1.</summary>
    public Guid WarehouseAdminId { get; private set; }
    public Guid BranchAdminId { get; private set; }
    public Guid ViewOnlyId { get; private set; }

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();
        await SeedAsync();

        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableUser.Role = Role;
        JobCompanyContext.Current = Guid.Empty;
        JobBranchContext.Current = Guid.Empty;
    }

    public async Task DisposeAsync() => await _baseFactory.DisposeAsync();

    /// <summary>El ICurrentUser del factory es compartido: cada actor lo fija antes de su solicitud.</summary>
    public HttpClient CreateClientFor(Guid userId)
    {
        _baseFactory.MutableUser.UserId = userId;
        _baseFactory.MutableUser.Role = Role;
        var client = _baseFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, userId, role: Role)
        );
        client.DefaultRequestHeaders.Add("X-Company-Id", CompanyAId.ToString());
        client.DefaultRequestHeaders.Add("X-Branch-Id", BranchA1Id.ToString());
        return client;
    }

    public IServiceScope CreateScope() => _baseFactory.Services.CreateScope();

    private static Branch NewBranch(Guid tenantId, Guid companyId, string name, string code, bool isMain, Guid by) =>
        Branch.Create(
            tenantId,
            name,
            "Av. Principal 123",
            code,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            isMainBranch: isMain,
            by,
            companyId: companyId
        );

    private Warehouse NewWarehouse(Guid companyId, Guid branchId, string code, bool isMain) =>
        Warehouse.Create(
            TenantId,
            branchId,
            $"Bodega {code}",
            code,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _adminId,
            companyId,
            isMain: isMain
        );

    private async Task SeedAsync()
    {
        using var scope = _baseFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        _adminId = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-CatalogAccess", $"zh-bca-{Guid.NewGuid():N}", _adminId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var companyA = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{Guid.NewGuid():N}"[..13],
            legalName: "Empresa A S.A.",
            createdBy: _adminId
        );
        var companyB = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{Guid.NewGuid():N}"[..13],
            legalName: "Empresa B S.A.",
            createdBy: _adminId
        );
        db.Companies.AddRange(companyA, companyB);
        await db.SaveChangesAsync();
        CompanyAId = companyA.Id;

        db.CompanyPrecisionPolicies.Add(
            CompanyPrecisionPolicy.CreateStandardCommercial(TenantId, CompanyAId, _adminId)
        );

        var branchA1 = NewBranch(TenantId, CompanyAId, "Matriz A", "SUC-A1", true, _adminId);
        var branchA2 = NewBranch(TenantId, CompanyAId, "Sucursal A2", "SUC-A2", false, _adminId);
        var branchB = NewBranch(TenantId, companyB.Id, "Matriz B", "SUC-B", true, _adminId);
        db.Branches.AddRange(branchA1, branchA2, branchB);
        await db.SaveChangesAsync();
        BranchA1Id = branchA1.Id;
        BranchA2Id = branchA2.Id;
        ForeignBranchId = branchB.Id;

        var mainA1 = NewWarehouse(CompanyAId, BranchA1Id, "A1-MAIN", isMain: true);
        var mainA2 = NewWarehouse(CompanyAId, BranchA2Id, "A2-MAIN", isMain: true);
        var secondaryA2 = NewWarehouse(CompanyAId, BranchA2Id, "A2-SEC", isMain: false);
        var warehouseB = NewWarehouse(companyB.Id, ForeignBranchId, "B-MAIN", isMain: true);
        db.Warehouses.AddRange(mainA1, mainA2, secondaryA2, warehouseB);
        await db.SaveChangesAsync();
        WarehouseA2Id = mainA2.Id;
        WarehouseA2ForDisableId = secondaryA2.Id;
        WarehouseBId = warehouseB.Id;

        WarehouseAdminId = await SeedUserAsync(
            db,
            "Administrador de bodegas",
            [
                "inventory.warehouses.view",
                "inventory.warehouses.create",
                "inventory.warehouses.update",
                "inventory.warehouses.delete",
            ]
        );
        BranchAdminId = await SeedUserAsync(
            db,
            "Administrador de sucursales",
            ["settings.branches.view", "settings.branches.update"]
        );
        // Equivalente al perfil semilla DataEntry: solo lectura del catálogo.
        ViewOnlyId = await SeedUserAsync(
            db,
            "Solo lectura",
            ["inventory.warehouses.view", "settings.branches.view"]
        );
    }

    private async Task<Guid> SeedUserAsync(ErpDbContext db, string profileName, string[] permissions)
    {
        var profile = AccessProfile.Create(TenantId, CompanyAId, profileName, null, _adminId);
        db.AccessProfiles.Add(profile);
        await db.SaveChangesAsync();
        foreach (var key in permissions)
            db.AccessProfilePermissions.Add(
                AccessProfilePermission.Create(TenantId, profile.Id, key, true, _adminId)
            );

        var user = IdentityUser.Create(
            $"usuario-{Guid.NewGuid():N}",
            "Usuario",
            "Prueba",
            $"usuario-{Guid.NewGuid():N}@example.com",
            "hash",
            _adminId
        );
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();

        var membership = CompanyUserMembership.Create(CompanyAId, user.Id, Role, profile.Id, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();
        db.CompanyUserBranches.Add(
            CompanyUserBranch.Create(TenantId, CompanyAId, membership.Id, BranchA1Id, _adminId)
        );
        await db.SaveChangesAsync();
        return user.Id;
    }
}

[Trait("Category", "PostgreSql")]
public sealed class BranchWarehouseCatalogAccessHttpTests
    : IClassFixture<BranchWarehouseCatalogAccessFixture>
{
    private const string Warehouses = "/api/v1/inventory/warehouses";
    private const string Branches = "/api/v1/settings/branches";

    private readonly BranchWarehouseCatalogAccessFixture _f;

    public BranchWarehouseCatalogAccessHttpTests(BranchWarehouseCatalogAccessFixture fixture) =>
        _f = fixture;

    private static async Task<string> AssertStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected, body);
        return body;
    }

    private static IEnumerable<Guid> ReadIds(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return doc
            .RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(e => e.GetProperty("id").GetGuid())
            .ToList();
    }

    private static object WarehousePayload(Guid? id, Guid branchId, string name) =>
        new
        {
            id,
            branchId,
            name,
            storageType = (string?)null,
            address = (string?)null,
            phone = (string?)null,
            email = (string?)null,
            manager = (string?)null,
            latitude = (string?)null,
            longitude = (string?)null,
            capacity = (decimal?)null,
            dailyDispatchGoal = (decimal?)null,
        };

    private static object BranchPayload(Guid id, string name) =>
        new
        {
            id,
            name,
            address = "Av. Principal 123",
            isActive = true,
            isMainBranch = false,
        };

    private async Task<(string Name, bool IsActive)> LoadWarehouseAsync(Guid id)
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var w = await db.Warehouses.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
        return (w.Name, w.IsActive);
    }

    private async Task<string> LoadBranchNameAsync(Guid id)
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await db.Branches.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => b.Name)
            .SingleAsync();
    }

    [Fact]
    public async Task Bodegas_alta_y_edicion_son_company_wide_por_permiso_y_deshabilitar_es_branch_scoped()
    {
        using var client = _f.CreateClientFor(_f.WarehouseAdminId);

        // El listado incluye bodegas de A2 aunque el usuario solo tenga asignada A1.
        var list = await AssertStatusAsync(await client.GetAsync(Warehouses), HttpStatusCode.OK);
        ReadIds(list).Should().Contain(_f.WarehouseA2Id).And.NotContain(_f.WarehouseBId);

        // Alta y edición en A2: autorizadas por inventory.warehouses.*.
        await AssertStatusAsync(
            await client.PostAsJsonAsync(Warehouses, WarehousePayload(null, _f.BranchA2Id, "Nueva A2")),
            HttpStatusCode.Created
        );
        await AssertStatusAsync(
            await client.PutAsJsonAsync(
                $"{Warehouses}/{_f.WarehouseA2Id}",
                WarehousePayload(_f.WarehouseA2Id, _f.BranchA2Id, "Bodega A2 renombrada")
            ),
            HttpStatusCode.OK
        );
        (await LoadWarehouseAsync(_f.WarehouseA2Id)).Name.Should().Be("Bodega A2 renombrada");

        // Deshabilitar es branch-scoped: una bodega de A2 no existe para la sucursal activa A1.
        await AssertStatusAsync(
            await client.PatchAsync($"{Warehouses}/{_f.WarehouseA2ForDisableId}/disable", null),
            HttpStatusCode.NotFound
        );
        (await LoadWarehouseAsync(_f.WarehouseA2ForDisableId)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Bodega_o_sucursal_de_otra_empresa_no_existe_para_la_operacion()
    {
        using var client = _f.CreateClientFor(_f.WarehouseAdminId);
        var before = await LoadWarehouseAsync(_f.WarehouseBId);

        await AssertStatusAsync(
            await client.PutAsJsonAsync(
                $"{Warehouses}/{_f.WarehouseBId}",
                WarehousePayload(_f.WarehouseBId, _f.ForeignBranchId, "Intrusión")
            ),
            HttpStatusCode.NotFound
        );
        await AssertStatusAsync(
            await client.PostAsJsonAsync(Warehouses, WarehousePayload(null, _f.ForeignBranchId, "Intrusión")),
            HttpStatusCode.UnprocessableEntity
        );
        (await LoadWarehouseAsync(_f.WarehouseBId)).Name.Should().Be(before.Name);
    }

    [Fact]
    public async Task Catalogo_de_sucursales_es_company_wide_por_permiso()
    {
        using var client = _f.CreateClientFor(_f.BranchAdminId);

        var list = await AssertStatusAsync(await client.GetAsync(Branches), HttpStatusCode.OK);
        ReadIds(list)
            .Should()
            .Contain([_f.BranchA1Id, _f.BranchA2Id])
            .And.NotContain(_f.ForeignBranchId);

        await AssertStatusAsync(
            await client.PutAsJsonAsync(
                $"{Branches}/{_f.BranchA2Id}",
                BranchPayload(_f.BranchA2Id, "Sucursal A2 renombrada")
            ),
            HttpStatusCode.OK
        );
        (await LoadBranchNameAsync(_f.BranchA2Id)).Should().Be("Sucursal A2 renombrada");

        await AssertStatusAsync(
            await client.PutAsJsonAsync(
                $"{Branches}/{_f.ForeignBranchId}",
                BranchPayload(_f.ForeignBranchId, "Intrusión")
            ),
            // Contrato actual de UpdateBranch: «Sucursal no encontrada.» se mapea a 400; sin fuga
            // de datos ni cambios (se verifica abajo).
            HttpStatusCode.BadRequest
        );
        (await LoadBranchNameAsync(_f.ForeignBranchId)).Should().Be("Matriz B");
    }

    [Fact]
    public async Task Sin_permiso_de_escritura_el_catalogo_solo_se_lee()
    {
        using var client = _f.CreateClientFor(_f.ViewOnlyId);

        await AssertStatusAsync(await client.GetAsync(Warehouses), HttpStatusCode.OK);
        await AssertStatusAsync(await client.GetAsync(Branches), HttpStatusCode.OK);

        await AssertStatusAsync(
            await client.PostAsJsonAsync(Warehouses, WarehousePayload(null, _f.BranchA1Id, "Sin permiso")),
            HttpStatusCode.Forbidden
        );
        await AssertStatusAsync(
            await client.PutAsJsonAsync(
                $"{Warehouses}/{_f.WarehouseA2Id}",
                WarehousePayload(_f.WarehouseA2Id, _f.BranchA2Id, "Sin permiso")
            ),
            HttpStatusCode.Forbidden
        );
        await AssertStatusAsync(
            await client.PatchAsync($"{Warehouses}/{_f.WarehouseA2ForDisableId}/disable", null),
            HttpStatusCode.Forbidden
        );
        await AssertStatusAsync(
            await client.PutAsJsonAsync(
                $"{Branches}/{_f.BranchA2Id}",
                BranchPayload(_f.BranchA2Id, "Sin permiso")
            ),
            HttpStatusCode.Forbidden
        );
        (await LoadWarehouseAsync(_f.WarehouseA2Id)).Name.Should().NotBe("Sin permiso");
        (await LoadBranchNameAsync(_f.BranchA2Id)).Should().NotBe("Sin permiso");
    }
}
