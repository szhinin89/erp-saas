using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Inventory.Stock.UseCases.CreateStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.PostInitialBalance;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// D-1 — <see cref="InventoryAdjustmentReason"/> es dato COMPANY. Prueba HTTP real contra PostgreSQL
/// (Testcontainers) con <c>CurrentCompanyService</c> real (X-Company-Id), mismo patrón que
/// <see cref="CompanyBranchIsolationHttpFixture"/>: el usuario solo tiene membership en Empresa A; los
/// motivos de Empresa B se siembran directamente y nunca deben ser visibles, modificables ni usables
/// desde A.
/// </summary>
public sealed class InventoryAdjustmentReasonCompanyIsolationFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new(useHttpCompanyContext: true);
    private Guid _adminId;

    public HttpClient Client { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public Guid CompanyAId { get; private set; }
    public Guid CompanyBId { get; private set; }
    public Guid BranchAId { get; private set; }
    public Guid WarehouseAId { get; private set; }
    public Guid ItemAId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ReasonBId { get; private set; }
    public const string ReasonBCode = "MERMA-B";

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();
        await SeedAsync();

        Client = CreateClient(CompanyAId);
        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableUser.UserId = UserId;
        JobCompanyContext.Current = Guid.Empty;
        JobBranchContext.Current = Guid.Empty;
    }

    public async Task DisposeAsync() => await _baseFactory.DisposeAsync();

    public HttpClient CreateClient(Guid? companyId)
    {
        var client = _baseFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, UserId)
        );
        if (companyId is { } id)
            client.DefaultRequestHeaders.Add("X-Company-Id", id.ToString());
        client.DefaultRequestHeaders.Add("X-Branch-Id", BranchAId.ToString());
        return client;
    }

    public IServiceScope CreateScope() => _baseFactory.Services.CreateScope();

    private async Task SeedAsync()
    {
        using var scope = _baseFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        _adminId = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-ReasonIsolation-Test", $"zh-ri-{Guid.NewGuid():N}", _adminId);
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
            ERP.Domain.Configuration.Entities.CompanyPrecisionPolicy.CreateStandardCommercial(
                TenantId,
                CompanyAId,
                _adminId
            )
        );
        await db.SaveChangesAsync();

        // Item levanta domain events cuyos *AuditHandler leen el contexto (mismo criterio que
        // InventoryAdjustmentsFlowFixture).
        _baseFactory.MutableTenant.TenantId = TenantId;
        JobCompanyContext.Current = CompanyAId;

        var branchA = Branch.Create(
            TenantId,
            "Matriz A",
            "Av. Principal 123",
            "SUC-A",
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
            isMainBranch: true,
            _adminId,
            companyId: CompanyAId
        );
        db.Branches.Add(branchA);
        await db.SaveChangesAsync();
        BranchAId = branchA.Id;

        var warehouse = Warehouse.Create(
            TenantId,
            BranchAId,
            "Bodega A",
            "BOD-A",
            storageType: null,
            address: null,
            phone: null,
            email: null,
            manager: null,
            latitude: null,
            longitude: null,
            capacity: null,
            dailyDispatchGoal: null,
            createdBy: _adminId,
            companyId: CompanyAId
        );
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();
        WarehouseAId = warehouse.Id;

        var itemType = ItemTypeDefinition.Create(TenantId, "MERCH", "Mercadería", 1, _adminId);
        db.Set<ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        var item = Item.Create(
            TenantId,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto A",
            description: "Producto A",
            itemTypeId: itemType.Id,
            defaultUomCode: "UNIT",
            taxConfig: ItemTaxConfig.Create(saleVatCode: null, purchaseVatCode: null),
            saleConfig: ItemSaleConfig.Create(isForSale: false),
            stockConfig: ItemStockConfig.Create(stockControlEnabled: true),
            createdBy: _adminId,
            companyId: CompanyAId
        );
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();
        item.ReplacePackagingLevels(
            new[]
            {
                (
                    Name: "Unidad",
                    Level: 1,
                    BaseQuantity: 1m,
                    UomCode: "UNIT",
                    Barcode: (string?)null,
                    Weight: (decimal?)null,
                    IsBaseUnit: true,
                    IsPurchaseDefault: false,
                    IsSaleDefault: false
                ),
            },
            _adminId
        );
        await db.SaveChangesAsync();
        ItemAId = item.Id;

        // Motivos de Empresa B (incluido CARGA_INICIAL) — sembrados directamente: el usuario nunca
        // tiene acceso a B.
        var reasonB = InventoryAdjustmentReason.Create(
            TenantId,
            CompanyBId,
            ReasonBCode,
            "Merma B",
            InventoryAdjustmentReason.Ambos,
            false,
            1,
            _adminId
        );
        db.Set<InventoryAdjustmentReason>()
            .AddRange(
                reasonB,
                InventoryAdjustmentReason.Create(
                    TenantId,
                    CompanyBId,
                    "CARGA_INICIAL",
                    "Carga Inicial B",
                    InventoryAdjustmentReason.Ingreso,
                    false,
                    0,
                    _adminId
                )
            );
        await db.SaveChangesAsync();
        ReasonBId = reasonB.Id;

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
        UserId = user.Id;

        // Membership solo en Empresa A.
        var membership = CompanyUserMembership.Create(CompanyAId, user.Id, "Admin", null, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();
        db.CompanyUserBranches.Add(
            CompanyUserBranch.Create(TenantId, CompanyAId, membership.Id, BranchAId, _adminId)
        );
        await db.SaveChangesAsync();
    }
}

[Trait("Category", "PostgreSql")]
public sealed class InventoryAdjustmentReasonCompanyIsolationHttpTests
    : IClassFixture<InventoryAdjustmentReasonCompanyIsolationFixture>
{
    private const string Reasons = "/api/v1/inventory/adjustment-reasons";
    private const string Adjustments = "/api/v1/inventory/stock/adjustments";
    private readonly InventoryAdjustmentReasonCompanyIsolationFixture _f;

    public InventoryAdjustmentReasonCompanyIsolationHttpTests(
        InventoryAdjustmentReasonCompanyIsolationFixture fixture
    ) => _f = fixture;

    private static object NewReason(string code, Guid? companyId = null) =>
        new
        {
            companyId,
            code,
            name = $"Motivo {code}",
            allowedMovementType = InventoryAdjustmentReason.Ambos,
            requiresNotes = false,
            sortOrder = 1,
        };

    private object NewAdjustment(Guid reasonId) =>
        new
        {
            warehouseId = _f.WarehouseAId,
            warehouseName = "Bodega A",
            movementType = "Ingreso",
            reasonId,
            notes = (string?)null,
            lines = new object[]
            {
                new
                {
                    itemId = _f.ItemAId,
                    itemName = "Producto A",
                    packagingLevelId = (Guid?)null,
                    quantity = 1m,
                    unitCostBase = 5m,
                    lineNotes = (string?)null,
                },
            },
        };

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task<InventoryAdjustmentReason> LoadReasonBAsync()
    {
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        return await db.Set<InventoryAdjustmentReason>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(r => r.Id == _f.ReasonBId);
    }

    [Fact]
    public async Task Listado_de_A_no_incluye_motivos_de_B_y_companyId_ajeno_en_query_es_403()
    {
        var created = await _f.Client.PostAsJsonAsync(Reasons, NewReason($"LST-{Guid.NewGuid():N}"[..14]));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var ownId = await ReadIdAsync(created);

        var list = await _f.Client.GetAsync($"{Reasons}?includeInactive=true");
        var body = await list.Content.ReadAsStringAsync();
        list.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().Contain(ownId.ToString());
        body.Should().NotContain(_f.ReasonBId.ToString());

        var foreign = await _f.Client.GetAsync($"{Reasons}?companyId={_f.CompanyBId}");
        foreign.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var ownHint = await _f.Client.GetAsync($"{Reasons}?companyId={_f.CompanyAId}");
        ownHint.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Crear_toma_la_empresa_del_contexto_y_companyId_ajeno_en_body_es_403()
    {
        var code = $"CRE-{Guid.NewGuid():N}"[..14];

        var foreign = await _f.Client.PostAsJsonAsync(Reasons, NewReason(code, _f.CompanyBId));
        foreign.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var created = await _f.Client.PostAsJsonAsync(Reasons, NewReason(code));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = await ReadIdAsync(created);

        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var rows = await db.Set<InventoryAdjustmentReason>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.TenantId == _f.TenantId && r.Code == code.ToUpperInvariant())
            .ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(id);
        rows[0].CompanyId.Should().Be(_f.CompanyAId);
    }

    [Fact]
    public async Task Mismo_codigo_incluido_CARGA_INICIAL_puede_existir_en_A_y_en_B()
    {
        // B ya tiene MERMA-B y CARGA_INICIAL (fixture). El código es único por Tenant + Company.
        var sameAsB = await _f.Client.PostAsJsonAsync(Reasons, NewReason(InventoryAdjustmentReasonCompanyIsolationFixture.ReasonBCode));
        sameAsB.StatusCode.Should().Be(HttpStatusCode.Created, await sameAsB.Content.ReadAsStringAsync());

        var cargaInicial = await _f.Client.PostAsJsonAsync(Reasons, NewReason("CARGA_INICIAL"));
        cargaInicial.StatusCode.Should().Be(HttpStatusCode.Created, await cargaInicial.Content.ReadAsStringAsync());

        var duplicateInA = await _f.Client.PostAsJsonAsync(Reasons, NewReason("CARGA_INICIAL"));
        duplicateInA.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task A_no_edita_desactiva_ni_configura_cuenta_de_un_motivo_de_B()
    {
        var update = await _f.Client.PutAsJsonAsync(
            $"{Reasons}/{_f.ReasonBId}",
            new
            {
                id = _f.ReasonBId,
                name = "Renombrado desde A",
                allowedMovementType = InventoryAdjustmentReason.Ambos,
                requiresNotes = false,
                sortOrder = 9,
            }
        );
        update.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var toggle = await _f.Client.PostAsJsonAsync($"{Reasons}/{_f.ReasonBId}/toggle", new { activate = false });
        toggle.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var account = await _f.Client.PutAsJsonAsync(
            $"{Reasons}/{_f.ReasonBId}/account",
            new { accountingAccountId = Guid.NewGuid() }
        );
        account.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var reasonB = await LoadReasonBAsync();
        reasonB.Name.Should().Be("Merma B");
        reasonB.IsActive.Should().BeTrue();
        reasonB.SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task A_no_crea_ajuste_con_motivo_de_B()
    {
        // Conteo antes/después: otro Fact del fixture compartido fuerza por SQL un borrador con este motivo.
        using var scope = _f.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var before = await db.StockAdjustments.IgnoreQueryFilters().CountAsync(a => a.TenantId == _f.TenantId);

        var response = await _f.Client.PostAsJsonAsync(Adjustments, NewAdjustment(_f.ReasonBId));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await response.Content.ReadAsStringAsync());

        (await db.StockAdjustments.IgnoreQueryFilters().CountAsync(a => a.TenantId == _f.TenantId))
            .Should().Be(before);
    }

    [Fact]
    public async Task Borrador_que_referencia_motivo_de_B_no_se_ejecuta_ni_mueve_stock_ni_contabiliza()
    {
        var reason = await _f.Client.PostAsJsonAsync(Reasons, NewReason($"EXE-{Guid.NewGuid():N}"[..14]));
        reason.StatusCode.Should().Be(HttpStatusCode.Created, await reason.Content.ReadAsStringAsync());
        var draft = await _f.Client.PostAsJsonAsync(Adjustments, NewAdjustment(await ReadIdAsync(reason)));
        draft.StatusCode.Should().Be(HttpStatusCode.Created, await draft.Content.ReadAsStringAsync());
        var draftId = await ReadIdAsync(draft);

        // Referencia cruzada forzada en la BD efímera del test (no alcanzable por la API corregida).
        using (var scope = _f.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE stock_adjustments SET reason_id = {_f.ReasonBId} WHERE id = {draftId}"
            );
        }

        var execute = await _f.Client.PostAsync($"{Adjustments}/{draftId}/execute", null);
        execute.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await execute.Content.ReadAsStringAsync());

        using var verify = _f.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await vdb.StockAdjustments.IgnoreQueryFilters().SingleAsync(a => a.Id == draftId))
            .Status.Should().Be("Draft");
        (await vdb.StockMovements.IgnoreQueryFilters().AnyAsync(m => m.SourceDocId == draftId))
            .Should().BeFalse();
        (await vdb.AccountingPostings.IgnoreQueryFilters().AnyAsync(p => p.SourceDocumentId == draftId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Saldo_inicial_en_A_rechaza_motivo_de_B()
    {
        using var scope = _f.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        JobCompanyContext.Current = _f.CompanyAId;
        JobBranchContext.Current = _f.BranchAId;
        try
        {
            var result = await mediator.Send(
                new PostInitialBalanceCommand(
                    _f.WarehouseAId,
                    "Bodega A",
                    _f.ReasonBId,
                    new DateOnly(2026, 1, 1),
                    null,
                    [new CreateStockAdjustmentLineInput(_f.ItemAId, "Producto A", null, 1m, 5m, null)]
                )
            );

            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("motivo de apertura no existe");
        }
        finally
        {
            JobCompanyContext.Current = Guid.Empty;
            JobBranchContext.Current = Guid.Empty;
        }

        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        (await db.StockMovements.IgnoreQueryFilters().AnyAsync(m => m.ProductId == _f.ItemAId && m.WarehouseId == _f.WarehouseAId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Sin_empresa_autorizada_o_sin_contexto_de_empresa_la_operacion_se_rechaza()
    {
        using var asB = _f.CreateClient(_f.CompanyBId);
        (await asB.GetAsync(Reasons)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await asB.PostAsJsonAsync(Reasons, NewReason($"NOB-{Guid.NewGuid():N}"[..14])))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await asB.PostAsJsonAsync($"{Reasons}/{_f.ReasonBId}/toggle", new { activate = false }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var noCompany = _f.CreateClient(null);
        (await noCompany.GetAsync(Reasons)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await LoadReasonBAsync()).IsActive.Should().BeTrue();
    }
}
