using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// WAREHOUSE-BRANCH-REASSIGNMENT-INTEGRITY-01 (opción B) — PUT /inventory/warehouses/{id} contra
/// PostgreSQL real: el cambio de sucursal solo se permite en una bodega sin existencias (positivas
/// o negativas), kardex, lotes/series, documentos ni referencias predeterminadas (sucursal, caja,
/// bodega principal); si no, 422 sin escrituras. Los demás campos siguen editables y otra empresa
/// sigue aislada.
/// </summary>
public sealed class WarehouseBranchReassignmentFixture : IAsyncLifetime
{
    public const string Role = "Cajero";

    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);
    private Guid _adminId;

    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid BranchA1Id { get; private set; }
    public Guid BranchA2Id { get; private set; }
    public Guid ForeignBranchId { get; private set; }
    public Guid WarehouseBId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ItemId { get; private set; }

    /// <summary>Bodegas de A1 por escenario: free, main, positive, negative, movement, branchDefault, cashDefault.</summary>
    public Dictionary<string, Guid> Warehouses { get; } = [];

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

    public HttpClient CreateClient()
    {
        _baseFactory.MutableUser.UserId = UserId;
        _baseFactory.MutableUser.Role = Role;
        var client = _baseFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, UserId, role: Role)
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

    private Warehouse NewWarehouse(Guid companyId, Guid branchId, string code, bool isMain = false) =>
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
        var tenant = Tenant.Create("ZH-WarehouseReassign", $"zh-wbr-{Guid.NewGuid():N}", _adminId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;
        // La auditoría de Item (interceptor) exige tenant y usuario actuales al guardar.
        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableUser.UserId = _adminId;

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

        foreach (var key in new[] { "free", "positive", "negative", "movement", "branchDefault", "cashDefault", "raceMovementFirst", "raceReassignFirst" })
        {
            var w = NewWarehouse(CompanyAId, BranchA1Id, $"A1-{key}");
            db.Warehouses.Add(w);
            Warehouses[key] = w.Id;
        }
        var main = NewWarehouse(CompanyAId, BranchA1Id, "A1-main", isMain: true);
        db.Warehouses.Add(main);
        Warehouses["main"] = main.Id;
        db.Warehouses.Add(NewWarehouse(CompanyAId, BranchA2Id, "A2-main", isMain: true));
        var warehouseB = NewWarehouse(companyB.Id, ForeignBranchId, "B-main", isMain: true);
        db.Warehouses.Add(warehouseB);
        await db.SaveChangesAsync();
        WarehouseBId = warehouseB.Id;

        var type = ItemTypeDefinition.Create(TenantId, "M" + CompanyAId.ToString("N")[..5], "Mercadería", 1, _adminId);
        db.Set<ItemTypeDefinition>().Add(type);
        await db.SaveChangesAsync();
        var item = Item.Create(TenantId, "WBR-" + CompanyAId.ToString("N")[..6], "Producto", "Producto", type.Id,
            "UNIT", ItemTaxConfig.Create(saleVatCode: "4", purchaseVatCode: "4"), ItemSaleConfig.Create(isForSale: true),
            ItemStockConfig.Create(stockControlEnabled: true), _adminId, companyId: CompanyAId);
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();
        ItemId = item.Id;

        // Existencias positivas y negativas (la negativa se fija con SQL: el dominio no la produce
        // con ApplyMovement, pero puede existir por venta negativa autorizada).
        var positive = CurrentStock.Create(TenantId, item.Id, Warehouses["positive"], _adminId, CompanyAId);
        positive.ApplyMovement(5m, _adminId);
        var negative = CurrentStock.Create(TenantId, item.Id, Warehouses["negative"], _adminId, CompanyAId);
        db.CurrentStocks.AddRange(positive, negative);
        db.StockMovements.Add(
            StockMovement.Create(
                TenantId,
                BranchA1Id,
                item.Id,
                Warehouses["movement"],
                StockMovementType.PositiveAdjust,
                1m,
                "UNIT",
                0m,
                1,
                0m,
                0m,
                new DateOnly(2026, 11, 1),
                "HIST",
                null,
                null,
                _adminId,
                CompanyAId
            )
        );
        db.OrgSettings.Add(
            OrgSetting.Create(
                TenantId,
                CompanyAId,
                OrgScope.Branch,
                BranchA1Id,
                OrgSettingKeys.Invoice.DefaultWarehouseId,
                Warehouses["branchDefault"].ToString(),
                SettingDataType.Guid,
                _adminId
            )
        );
        db.CashRegisters.Add(
            CashRegister.Create(
                TenantId,
                CompanyAId,
                BranchA1Id,
                "CAJA-01",
                "Caja 1",
                _adminId,
                defaultWarehouseId: Warehouses["cashDefault"]
            )
        );
        await db.SaveChangesAsync();
        var updated = await db.CurrentStocks.IgnoreQueryFilters()
            .Where(s => s.Id == negative.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, -3m));
        if (updated != 1)
            throw new InvalidOperationException("No se pudo sembrar la existencia negativa.");

        UserId = await SeedUserAsync(db);
    }

    private async Task<Guid> SeedUserAsync(ErpDbContext db)
    {
        var profile = AccessProfile.Create(TenantId, CompanyAId, "Administrador de bodegas", null, _adminId);
        db.AccessProfiles.Add(profile);
        await db.SaveChangesAsync();
        foreach (var key in new[] { "inventory.warehouses.view", "inventory.warehouses.update" })
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
public sealed class WarehouseBranchReassignmentHttpTests : IClassFixture<WarehouseBranchReassignmentFixture>
{
    private const string Warehouses = "/api/v1/inventory/warehouses";

    private readonly WarehouseBranchReassignmentFixture _f;

    public WarehouseBranchReassignmentHttpTests(WarehouseBranchReassignmentFixture fixture) => _f = fixture;

    private static object Payload(Guid id, Guid branchId, string name) =>
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

    private async Task<(Guid BranchId, string Name, int Activities)> LoadAsync(Guid id)
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var w = await db.Warehouses.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
        var activities = await db.UserActivities.IgnoreQueryFilters().CountAsync(a => a.EntityId == id);
        return (w.BranchId, w.Name, activities);
    }

    private async Task<string> PutAsync(Guid id, Guid branchId, string name, HttpStatusCode expected)
    {
        using var client = _f.CreateClient();
        var response = await client.PutAsJsonAsync($"{Warehouses}/{id}", Payload(id, branchId, name));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected, body);
        return body;
    }

    [Fact]
    public async Task Bodega_sin_historial_ni_dependencias_puede_cambiar_de_sucursal()
    {
        var id = _f.Warehouses["free"];

        await PutAsync(id, _f.BranchA2Id, "Bodega libre", HttpStatusCode.OK);

        (await LoadAsync(id)).BranchId.Should().Be(_f.BranchA2Id);
    }

    [Theory]
    [InlineData("positive", "tiene existencias")]
    [InlineData("negative", "tiene existencias")]
    [InlineData("movement", "movimientos de inventario")]
    [InlineData("branchDefault", "bodega predeterminada")]
    [InlineData("cashDefault", "de una caja")]
    [InlineData("main", "bodega principal")]
    public async Task Bodega_con_existencias_historial_o_referencias_rechaza_el_cambio_sin_escrituras(
        string scenario,
        string expectedReason
    )
    {
        var id = _f.Warehouses[scenario];
        var before = await LoadAsync(id);

        var body = await PutAsync(id, _f.BranchA2Id, "Nombre que no debe guardarse", HttpStatusCode.UnprocessableEntity);

        body.Should().Contain("No se puede cambiar la sucursal de la bodega").And.Contain(expectedReason);
        (await LoadAsync(id)).Should().Be(before);
    }

    [Fact]
    public async Task Bodega_con_historial_sigue_editable_sin_cambiar_de_sucursal()
    {
        var id = _f.Warehouses["movement"];

        await PutAsync(id, _f.BranchA1Id, "Bodega con kardex renombrada", HttpStatusCode.OK);

        var after = await LoadAsync(id);
        after.BranchId.Should().Be(_f.BranchA1Id);
        after.Name.Should().Be("Bodega con kardex renombrada");
    }

    [Fact]
    public async Task Aislamiento_entre_empresas_se_conserva()
    {
        var foreignBefore = await LoadAsync(_f.WarehouseBId);
        await PutAsync(_f.WarehouseBId, _f.BranchA1Id, "Intrusión", HttpStatusCode.NotFound);
        (await LoadAsync(_f.WarehouseBId)).Should().Be(foreignBefore);

        // Una bodega propia no puede moverse a una sucursal de otra empresa.
        var ownId = _f.Warehouses["free"];
        var ownBefore = await LoadAsync(ownId);
        await PutAsync(ownId, _f.ForeignBranchId, "Intrusión", HttpStatusCode.UnprocessableEntity);
        (await LoadAsync(ownId)).Should().Be(ownBefore);
    }
    // ── Concurrencia (PostgreSQL real) ─────────────────────────────────────

    private static readonly TimeSpan BlockedWindow = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Registra un movimiento de Kardex con el StockRepository real en un contexto de empresa aislado
    /// (AsyncLocal dentro de Task.Run). Tras AppendMovementAsync la transacción del movimiento queda
    /// abierta con sus locks hasta que se libera <paramref name="release"/>.
    /// </summary>
    private Task<Guid> RunMovementAsync(Guid warehouseId, TaskCompletionSource appended, Task release) =>
        Task.Run(async () =>
        {
            JobCompanyContext.Current = _f.CompanyAId;
            using var scope = _f.CreateScope();
            var stock = scope.ServiceProvider.GetRequiredService<IStockRepository>();
            var movement = await stock.AppendMovementAsync(
                _f.TenantId,
                _f.CompanyAId,
                _f.ItemId,
                warehouseId,
                StockMovementType.PositiveAdjust,
                1m,
                "UNIT",
                new DateOnly(2026, 11, 1),
                "RACE",
                null,
                null,
                _f.UserId,
                unitCost: 1m
            );
            appended.SetResult();
            await release;
            await stock.SaveChangesWithSequenceRetryAsync();
            return movement.Id;
        });

    private async Task<Guid> LoadMovementBranchAsync(Guid movementId)
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await db.StockMovements.IgnoreQueryFilters()
            .Where(m => m.Id == movementId)
            .Select(m => m.BranchId)
            .SingleAsync();
    }

    [Fact]
    public async Task Movimiento_en_curso_hace_esperar_la_reasignacion_que_luego_lo_rechaza()
    {
        var id = _f.Warehouses["raceMovementFirst"];
        var appended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var movementTask = RunMovementAsync(id, appended, release.Task);
        await appended.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // La transacción del movimiento sigue abierta: la reasignación no puede completarse.
        var putTask = PutAsync(id, _f.BranchA2Id, "Carrera A", HttpStatusCode.UnprocessableEntity);
        (await Task.WhenAny(putTask, Task.Delay(BlockedWindow))).Should().NotBeSameAs(putTask);

        release.SetResult();
        var movementId = await movementTask;
        var body = await putTask.WaitAsync(TimeSpan.FromSeconds(30));

        body.Should().Contain("No se puede cambiar la sucursal de la bodega");
        (await LoadAsync(id)).BranchId.Should().Be(_f.BranchA1Id);
        (await LoadMovementBranchAsync(movementId)).Should().Be(_f.BranchA1Id);
    }

    [Fact]
    public async Task Reasignacion_en_curso_hace_esperar_el_movimiento_que_registra_la_sucursal_nueva()
    {
        var id = _f.Warehouses["raceReassignFirst"];
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        // Misma secuencia que UpdateWarehouse al cambiar la sucursal: FOR UPDATE y UPDATE en una
        // transacción que se mantiene abierta.
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM warehouses WHERE id = {id} FOR UPDATE"
            );
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE warehouses SET branch_id = {_f.BranchA2Id} WHERE id = {id}"
            );

            var appended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var movementTask = RunMovementAsync(id, appended, Task.CompletedTask);
            (await Task.WhenAny(appended.Task, Task.Delay(BlockedWindow))).Should().NotBeSameAs(appended.Task);

            await tx.CommitAsync();
            var movementId = await movementTask.WaitAsync(TimeSpan.FromSeconds(30));

            (await LoadAsync(id)).BranchId.Should().Be(_f.BranchA2Id);
            (await LoadMovementBranchAsync(movementId)).Should().Be(_f.BranchA2Id);
        }
    }
}
