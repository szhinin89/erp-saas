using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
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
/// BRANCH-CONFIG-WAREHOUSE-UNSET-01 — PUT /org-config/branch/{id}/invoice-defaults contra
/// PostgreSQL real: valida la bodega al guardar (422 si no existe, está inactiva, es de otra
/// sucursal o de otra empresa), acepta null (sin configurar → fallback a Warehouse.IsMain),
/// conserva el fail-closed de lectura para filas corruptas y exige erp.companies.update
/// (contrato congelado; settings.branches.update no basta).
/// BRANCH-CONFIG-CROSS-BRANCH-ACCESS-01: GET y PUT autorizan la sucursal de la ruta con
/// IBranchAccessGuard.RequireBranchAsync — sin CompanyUserBranch 403 (aunque la sucursal activa sea
/// válida), sucursal deshabilitada 403, ajena 404; el administrador global en modo operador
/// conserva el acceso.
/// </summary>
public sealed class BranchDefaultWarehouseFixture : IAsyncLifetime
{
    public const string Role = "Cajero";

    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);
    private Guid _adminId;

    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid CompanyBId { get; private set; }
    public Guid BranchA1Id { get; private set; }
    public Guid BranchA2Id { get; private set; }
    public Guid DisabledBranchId { get; private set; }
    public Guid ForeignBranchId { get; private set; }
    public Guid MainWarehouseA1Id { get; private set; }
    public Guid SecondaryWarehouseA1Id { get; private set; }
    public Guid InactiveWarehouseA1Id { get; private set; }
    public Guid MainWarehouseA2Id { get; private set; }
    public Guid WarehouseBId { get; private set; }
    public Guid EditorUserId { get; private set; }
    public Guid BranchesOnlyUserId { get; private set; }
    public Guid RestrictedUserId { get; private set; }
    public Guid GlobalAdminId { get; private set; }

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
    public HttpClient CreateClientFor(Guid userId, Guid branchId) =>
        CreateClient(userId, branchId, Role, TestJwtFactory.CreateSessionJwt(TenantId, userId, role: Role));

    /// <summary>Administrador global en modo operador (mismo patrón que CrossCompanyAdminPrivilegeHttpTests).</summary>
    public HttpClient CreateOperatorClient(Guid branchId) =>
        CreateClient(
            GlobalAdminId,
            branchId,
            "Admin",
            TestJwtFactory.CreateSessionJwt(
                TenantId,
                GlobalAdminId,
                role: "Admin",
                extraClaims:
                [
                    new Claim("operator_mode", "true"),
                    new Claim("global_admin_user_id", GlobalAdminId.ToString()),
                ]
            )
        );

    private HttpClient CreateClient(Guid userId, Guid branchId, string role, string jwt)
    {
        _baseFactory.MutableUser.UserId = userId;
        _baseFactory.MutableUser.Role = role;
        var client = _baseFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        client.DefaultRequestHeaders.Add("X-Company-Id", CompanyAId.ToString());
        client.DefaultRequestHeaders.Add("X-Branch-Id", branchId.ToString());
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
        var tenant = Tenant.Create("ZH-BranchDefaultWarehouse", $"zh-bdw-{Guid.NewGuid():N}", _adminId);
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
        CompanyBId = companyB.Id;

        db.CompanyPrecisionPolicies.Add(
            CompanyPrecisionPolicy.CreateStandardCommercial(TenantId, CompanyAId, _adminId)
        );

        var branchA1 = NewBranch(TenantId, CompanyAId, "Matriz A", "SUC-A1", true, _adminId);
        var branchA2 = NewBranch(TenantId, CompanyAId, "Sucursal A2", "SUC-A2", false, _adminId);
        var branchA3 = NewBranch(TenantId, CompanyAId, "Sucursal A3", "SUC-A3", false, _adminId);
        branchA3.Disable(_adminId);
        var branchB = NewBranch(TenantId, CompanyBId, "Matriz B", "SUC-B", true, _adminId);
        db.Branches.AddRange(branchA1, branchA2, branchA3, branchB);
        await db.SaveChangesAsync();
        BranchA1Id = branchA1.Id;
        BranchA2Id = branchA2.Id;
        DisabledBranchId = branchA3.Id;
        ForeignBranchId = branchB.Id;

        var mainA1 = NewWarehouse(CompanyAId, BranchA1Id, "A1-MAIN", isMain: true);
        var secondaryA1 = NewWarehouse(CompanyAId, BranchA1Id, "A1-SEC", isMain: false);
        var inactiveA1 = NewWarehouse(CompanyAId, BranchA1Id, "A1-OFF", isMain: false);
        inactiveA1.Disable(_adminId);
        var mainA2 = NewWarehouse(CompanyAId, BranchA2Id, "A2-MAIN", isMain: true);
        var warehouseB = NewWarehouse(CompanyBId, branchB.Id, "B-MAIN", isMain: true);
        db.Warehouses.AddRange(mainA1, secondaryA1, inactiveA1, mainA2, warehouseB);
        await db.SaveChangesAsync();
        MainWarehouseA1Id = mainA1.Id;
        SecondaryWarehouseA1Id = secondaryA1.Id;
        InactiveWarehouseA1Id = inactiveA1.Id;
        MainWarehouseA2Id = mainA2.Id;
        WarehouseBId = warehouseB.Id;

        // Configuración corrupta sembrada directamente (sin pasar por el handler): la sucursal A2
        // apunta a la bodega de Empresa B. La lectura debe seguir siendo fail-closed.
        db.OrgSettings.Add(
            OrgSetting.Create(
                TenantId,
                CompanyAId,
                OrgScope.Branch,
                BranchA2Id,
                OrgSettingKeys.Invoice.DefaultWarehouseId,
                WarehouseBId.ToString(),
                SettingDataType.Guid,
                _adminId
            )
        );
        await db.SaveChangesAsync();

        EditorUserId = await SeedUserAsync(
            db,
            "Editor configuración",
            ["erp.companies.update", "settings.company.view"]
        );
        BranchesOnlyUserId = await SeedUserAsync(
            db,
            "Editor sucursales",
            ["settings.branches.view", "settings.branches.update", "settings.company.view"]
        );
        // Mismos permisos que el editor, pero CompanyUserBranch solo en A1.
        RestrictedUserId = await SeedUserAsync(
            db,
            "Editor limitado a A1",
            ["erp.companies.update", "settings.company.view"],
            [BranchA1Id]
        );

        var globalAdmin = IdentityUser.Create(
            $"global-{Guid.NewGuid():N}",
            "Global",
            "Admin",
            $"global-{Guid.NewGuid():N}@example.com",
            "hash",
            _adminId
        );
        db.IdentityUsers.Add(globalAdmin);
        db.GlobalUserRoles.Add(GlobalUserRole.Create(globalAdmin.Id, "Admin", _adminId));
        await db.SaveChangesAsync();
        GlobalAdminId = globalAdmin.Id;
    }

    private async Task<Guid> SeedUserAsync(
        ErpDbContext db,
        string profileName,
        string[] permissions,
        Guid[]? branchIds = null
    )
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
        foreach (var branchId in branchIds ?? [BranchA1Id, BranchA2Id])
            db.CompanyUserBranches.Add(
                CompanyUserBranch.Create(TenantId, CompanyAId, membership.Id, branchId, _adminId)
            );
        await db.SaveChangesAsync();
        return user.Id;
    }
}

[Trait("Category", "PostgreSql")]
public sealed class BranchDefaultWarehouseHttpTests : IClassFixture<BranchDefaultWarehouseFixture>
{
    private const string SalesInvoiceDefaults = "/api/v1/sales/invoice-defaults";

    private readonly BranchDefaultWarehouseFixture _f;

    public BranchDefaultWarehouseHttpTests(BranchDefaultWarehouseFixture fixture) => _f = fixture;

    private static string BranchDefaults(Guid branchId) =>
        $"/api/v1/org-config/branch/{branchId}/invoice-defaults";

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("data").Clone();
    }

    private static Guid? ReadGuid(JsonElement data, string property) =>
        data.GetProperty(property).ValueKind == JsonValueKind.Null
            ? null
            : data.GetProperty(property).GetGuid();

    private async Task<OrgSetting?> LoadA1SettingAsync()
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await db.OrgSettings.IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(s =>
                s.TenantId == _f.TenantId
                && s.CompanyId == _f.CompanyAId
                && s.Scope == OrgScope.Branch
                && s.ScopeId == _f.BranchA1Id
                && s.Key == OrgSettingKeys.Invoice.DefaultWarehouseId
            );
    }

    private static object Payload(Guid branchId, Guid? warehouseId) =>
        new { branchId, defaultWarehouseId = warehouseId };

    [Fact]
    public async Task Bodega_por_defecto_valida_al_guardar_y_null_vuelve_a_la_bodega_principal()
    {
        using var client = _f.CreateClientFor(_f.EditorUserId, _f.BranchA1Id);

        // Sin preferencia explícita → bodega principal de la sucursal.
        var initial = await ReadDataAsync(await client.GetAsync(SalesInvoiceDefaults));
        ReadGuid(initial, "defaultWarehouseId").Should().Be(_f.MainWarehouseA1Id);
        initial.GetProperty("defaultWarehouseSource").GetString().Should().Be("BranchMainWarehouse");

        // Bodega activa de la sucursal → se guarda y la resolución la usa.
        var saved = await client.PutAsJsonAsync(
            BranchDefaults(_f.BranchA1Id),
            Payload(_f.BranchA1Id, _f.SecondaryWarehouseA1Id)
        );
        ReadGuid(await ReadDataAsync(saved), "defaultWarehouseId").Should().Be(_f.SecondaryWarehouseA1Id);
        var configured = await ReadDataAsync(await client.GetAsync(SalesInvoiceDefaults));
        ReadGuid(configured, "defaultWarehouseId").Should().Be(_f.SecondaryWarehouseA1Id);
        configured.GetProperty("defaultWarehouseSource").GetString().Should().Be("BranchSetting");

        // Inactiva, de otra sucursal, de otra empresa o inexistente → 422 y nada cambia.
        foreach (
            var invalid in new[]
            {
                _f.InactiveWarehouseA1Id,
                _f.MainWarehouseA2Id,
                _f.WarehouseBId,
                Guid.NewGuid(),
            }
        )
        {
            var rejected = await client.PutAsJsonAsync(
                BranchDefaults(_f.BranchA1Id),
                Payload(_f.BranchA1Id, invalid)
            );
            var body = await rejected.Content.ReadAsStringAsync();
            rejected.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, body);
            body.Should().NotContain(_f.WarehouseBId.ToString());
            (await LoadA1SettingAsync())!.Value.Should().Be(_f.SecondaryWarehouseA1Id.ToString());
        }

        // null («Sin configurar») → se guarda, la fila se conserva con valor null y vuelve el
        // fallback a la bodega principal.
        var unset = await client.PutAsJsonAsync(
            BranchDefaults(_f.BranchA1Id),
            Payload(_f.BranchA1Id, null)
        );
        ReadGuid(await ReadDataAsync(unset), "defaultWarehouseId").Should().BeNull();
        var row = await LoadA1SettingAsync();
        row.Should().NotBeNull();
        row!.Value.Should().BeNull();

        var branchView = await ReadDataAsync(await client.GetAsync(BranchDefaults(_f.BranchA1Id)));
        ReadGuid(branchView, "defaultWarehouseId").Should().BeNull();
        var fallback = await ReadDataAsync(await client.GetAsync(SalesInvoiceDefaults));
        ReadGuid(fallback, "defaultWarehouseId").Should().Be(_f.MainWarehouseA1Id);
        fallback.GetProperty("defaultWarehouseSource").GetString().Should().Be("BranchMainWarehouse");
        fallback.GetProperty("requiresManualWarehouseSelection").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Configuracion_corrupta_existente_sigue_siendo_fail_closed()
    {
        // A2 tiene sembrada la bodega de Empresa B: no cae a su bodega principal.
        using var client = _f.CreateClientFor(_f.EditorUserId, _f.BranchA2Id);

        var data = await ReadDataAsync(await client.GetAsync(SalesInvoiceDefaults));

        ReadGuid(data, "defaultWarehouseId").Should().BeNull();
        data.GetProperty("defaultWarehouseSource").GetString().Should().Be("None");
        data.GetProperty("requiresManualWarehouseSelection").GetBoolean().Should().BeTrue();
        data.GetProperty("configurationWarnings").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Sin_erp_companies_update_el_guardado_se_rechaza_aunque_tenga_settings_branches_update()
    {
        var before = (await LoadA1SettingAsync())?.Value;
        using var client = _f.CreateClientFor(_f.BranchesOnlyUserId, _f.BranchA1Id);

        var response = await client.PutAsJsonAsync(
            BranchDefaults(_f.BranchA1Id),
            Payload(_f.BranchA1Id, _f.SecondaryWarehouseA1Id)
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await LoadA1SettingAsync())?.Value.Should().Be(before);
    }

    private async Task<string?> LoadBranchSettingAsync(Guid branchId)
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await db.OrgSettings.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s =>
                s.TenantId == _f.TenantId
                && s.Scope == OrgScope.Branch
                && s.ScopeId == branchId
                && s.Key == OrgSettingKeys.Invoice.DefaultWarehouseId
            )
            .Select(s => s.Value)
            .SingleOrDefaultAsync();
    }

    private static async Task AssertStatusAsync(
        HttpResponseMessage response,
        HttpStatusCode expected,
        string? mustNotContain = null
    )
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected, body);
        if (mustNotContain is not null)
            body.Should().NotContain(mustNotContain);
    }

    [Fact]
    public async Task Usuario_limitado_a_A1_no_lee_ni_modifica_la_configuracion_de_A2()
    {
        // Sucursal activa A1 válida: el rechazo proviene de la sucursal de la ruta (A2).
        var before = await LoadBranchSettingAsync(_f.BranchA2Id);
        using var client = _f.CreateClientFor(_f.RestrictedUserId, _f.BranchA1Id);

        await AssertStatusAsync(
            await client.GetAsync(BranchDefaults(_f.BranchA2Id)),
            HttpStatusCode.Forbidden,
            _f.WarehouseBId.ToString()
        );
        await AssertStatusAsync(
            await client.PutAsJsonAsync(
                BranchDefaults(_f.BranchA2Id),
                Payload(_f.BranchA2Id, null)
            ),
            HttpStatusCode.Forbidden
        );
        (await LoadBranchSettingAsync(_f.BranchA2Id)).Should().Be(before);

        // Su propia sucursal sigue accesible.
        await AssertStatusAsync(
            await client.GetAsync(BranchDefaults(_f.BranchA1Id)),
            HttpStatusCode.OK
        );
    }

    [Fact]
    public async Task Sucursal_deshabilitada_y_sucursal_de_otra_empresa_se_rechazan()
    {
        using var client = _f.CreateClientFor(_f.EditorUserId, _f.BranchA1Id);

        await AssertStatusAsync(
            await client.GetAsync(BranchDefaults(_f.DisabledBranchId)),
            HttpStatusCode.Forbidden
        );
        await AssertStatusAsync(
            await client.PutAsJsonAsync(
                BranchDefaults(_f.DisabledBranchId),
                Payload(_f.DisabledBranchId, null)
            ),
            HttpStatusCode.Forbidden
        );
        (await LoadBranchSettingAsync(_f.DisabledBranchId)).Should().BeNull();

        await AssertStatusAsync(
            await client.GetAsync(BranchDefaults(_f.ForeignBranchId)),
            HttpStatusCode.NotFound,
            _f.WarehouseBId.ToString()
        );
        await AssertStatusAsync(
            await client.PutAsJsonAsync(
                BranchDefaults(_f.ForeignBranchId),
                Payload(_f.ForeignBranchId, null)
            ),
            HttpStatusCode.NotFound
        );
    }

    [Fact]
    public async Task Administrador_global_en_modo_operador_conserva_acceso_a_cualquier_sucursal()
    {
        using var client = _f.CreateOperatorClient(_f.BranchA1Id);

        var read = await ReadDataAsync(await client.GetAsync(BranchDefaults(_f.BranchA2Id)));
        ReadGuid(read, "defaultWarehouseId").Should().Be(_f.WarehouseBId);

        // Guardar la bodega principal de A2 corrige la configuración corrupta sembrada; se
        // restaura después para no alterar el caso fail-closed de esta misma clase.
        var saved = await client.PutAsJsonAsync(
            BranchDefaults(_f.BranchA2Id),
            Payload(_f.BranchA2Id, _f.MainWarehouseA2Id)
        );
        ReadGuid(await ReadDataAsync(saved), "defaultWarehouseId").Should().Be(_f.MainWarehouseA2Id);

        using (var scope = _f.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var row = await db.OrgSettings.IgnoreQueryFilters()
                .SingleAsync(s =>
                    s.TenantId == _f.TenantId
                    && s.ScopeId == _f.BranchA2Id
                    && s.Key == OrgSettingKeys.Invoice.DefaultWarehouseId
                );
            row.UpdateValue(_f.WarehouseBId.ToString(), Guid.NewGuid());
            await db.SaveChangesAsync();
        }
    }
}
