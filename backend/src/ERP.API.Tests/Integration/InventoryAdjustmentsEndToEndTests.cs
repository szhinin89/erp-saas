using ERP.API.Tests.Support;
using ERP.Domain.Access.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
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
/// INVENTORY-ADJUSTMENTS-04 — validación funcional end-to-end del módulo "Ajustes de inventario"
/// contra PostgreSQL real (Testcontainers) y el pipeline HTTP completo (controllers, MediatR,
/// FluentValidation, EF Core). Sigue el mismo patrón que <see cref="CajaVentasEndToEndTests"/> y
/// <see cref="SalesReturnEndToEndTests"/> — reutiliza <c>PostgreSqlTestWebAppFactory</c> y
/// <c>TestJwtFactory</c> tal cual, sin infraestructura de test nueva (salvo un cliente HTTP
/// adicional por escenario para probar permisos granulares — ver Escenario7).
///
/// Cubre los 7 escenarios canónicos del prompt INVENTORY-ADJUSTMENTS-04: Motivos, Ingreso base,
/// Egreso base, Stock insuficiente, Presentación (Caja x12), Anulación, Bloqueos de estado/permisos.
///
/// Diseño deliberado: cada Fact crea su PROPIO motivo (código único por test) y lee saldos vía
/// "antes/después" reales (nunca valores absolutos hardcodeados que asuman orden de ejecución) —
/// xUnit no garantiza el orden de los [Fact] dentro de una clase con IClassFixture, así que el
/// ítem/bodega compartidos por el fixture pueden acumular movimientos de otros Facts.
/// </summary>
public sealed class InventoryAdjustmentsFlowFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _baseFactory = new();

    public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory => _baseFactory;
    public HttpClient Client { get; private set; } = null!;

    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ItemId { get; private set; }
    public string BaseUomCode { get; private set; } = "UNIT";
    public Guid BasePackagingLevelId { get; private set; }
    public Guid BoxPackagingLevelId { get; private set; }
    public decimal BoxConversionFactor { get; private set; } = 12m;

    private Guid _adminId;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("JWT__SECRETKEY", IntegrationTestConstants.JwtSecretKey);
        Environment.SetEnvironmentVariable("JWT__ISSUER", "ZHTechnologies");
        Environment.SetEnvironmentVariable("JWT__AUDIENCE", "ERPUsers");

        await _baseFactory.InitializeAsync();
        await _baseFactory.MigrateAsync();
        await SeedAsync();

        var adminUserId = await CreateUserWithBranchAccessAsync("Admin", null);

        Client = Factory.CreateClient();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, adminUserId)
        );
        Client.DefaultRequestHeaders.Add("X-Branch-Id", BranchId.ToString());

        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableCompany.CompanyId = CompanyId;
        _baseFactory.MutableUser.UserId = adminUserId;
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();

    private async Task SeedAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        _adminId = Guid.NewGuid();
        var tenant = Tenant.Create(
            "ZH-InvAdjustments-Test",
            $"zh-ia-{Guid.NewGuid():N}",
            _adminId
        );
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        var company = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"179{TenantId:N}"[..13],
            legalName: "Empresa InvAdjustments S.A.",
            createdBy: _adminId
        );
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        CompanyId = company.Id;

        // Fijar el contexto tenant/company mutable ANTES de seguir sembrando: Item levanta domain
        // events cuyos *AuditHandler leen ICurrentTenant/ICurrentCompany (mismo criterio que
        // SalesReturnFlowFixture).
        _baseFactory.MutableTenant.TenantId = TenantId;
        _baseFactory.MutableCompany.CompanyId = CompanyId;

        var branch = Branch.Create(
            tenantId: TenantId,
            name: "Matriz",
            address: "Av. Principal 123",
            code: "SUC-A",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: _adminId,
            companyId: CompanyId
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        BranchId = branch.Id;

        var warehouse = Warehouse.Create(
            TenantId,
            BranchId,
            "Bodega Principal",
            "BOD-01",
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
            companyId: CompanyId
        );
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();
        WarehouseId = warehouse.Id;

        var itemType = ItemTypeDefinition.Create(TenantId, "MERCH", "Mercadería", 1, _adminId);
        db.Set<ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        var item = Item.Create(
            TenantId,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto Ajustes E2E",
            description: "Producto Ajustes E2E",
            itemTypeId: itemType.Id,
            defaultUomCode: BaseUomCode,
            taxConfig: ItemTaxConfig.Create(saleVatCode: null, purchaseVatCode: null),
            saleConfig: ItemSaleConfig.Create(isForSale: false),
            stockConfig: ItemStockConfig.Create(tracksStock: true),
            createdBy: _adminId
        );
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();
        ItemId = item.Id;

        item.ReplacePackagingLevels(
            new[]
            {
                (
                    Name: "Unidad",
                    Level: 1,
                    BaseQuantity: 1m,
                    UomCode: BaseUomCode,
                    Barcode: (string?)null,
                    Weight: (decimal?)null,
                    IsBaseUnit: true,
                    IsPurchaseDefault: false,
                    IsSaleDefault: false
                ),
                (
                    Name: "Caja x12",
                    Level: 2,
                    BaseQuantity: BoxConversionFactor,
                    UomCode: "BOX",
                    Barcode: (string?)null,
                    Weight: (decimal?)null,
                    IsBaseUnit: false,
                    IsPurchaseDefault: false,
                    IsSaleDefault: false
                ),
            },
            _adminId
        );
        await db.SaveChangesAsync();

        BasePackagingLevelId = item.PackagingLevels.Single(l => l.IsBaseUnit).Id;
        BoxPackagingLevelId = item.PackagingLevels.Single(l => l.UomCode == "BOX").Id;
    }

    /// <summary>
    /// Crea un usuario real con membresía y acceso autorizado a la sucursal (CompanyUserBranch) —
    /// mismo patrón que CajaVentasEndToEndTests/SalesReturnEndToEndTests. Si <paramref name="profileId"/>
    /// se especifica, la membresía queda ligada a ese AccessProfile (permisos granulares —
    /// Escenario7); si es null, el rol pasado gobierna (Admin bypasea todo perm: check).
    /// </summary>
    public async Task<Guid> CreateUserWithBranchAccessAsync(string role, Guid? profileId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var user = IdentityUser.Create(
            $"user-{Guid.NewGuid():N}",
            "Usuario",
            "E2E",
            $"user-{Guid.NewGuid():N}@example.com",
            "hash",
            _adminId
        );
        db.IdentityUsers.Add(user);
        await db.SaveChangesAsync();

        var membership = CompanyUserMembership.Create(CompanyId, user.Id, role, profileId, _adminId);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();

        db.CompanyUserBranches.Add(
            CompanyUserBranch.Create(TenantId, CompanyId, membership.Id, BranchId, _adminId)
        );
        await db.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>Crea un AccessProfile con exactamente los permisos indicados (IsAllowed=true).</summary>
    public async Task<Guid> CreateProfileWithPermissionsAsync(string name, params string[] permissionKeys)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var profile = AccessProfile.Create(TenantId, name, null, _adminId);
        db.AccessProfiles.Add(profile);
        await db.SaveChangesAsync();

        foreach (var key in permissionKeys)
            db.AccessProfilePermissions.Add(
                AccessProfilePermission.Create(TenantId, profile.Id, key, true, _adminId)
            );
        await db.SaveChangesAsync();

        return profile.Id;
    }

    /// <summary>
    /// Cliente HTTP dedicado a un usuario/rol distinto del admin del fixture (Escenario7 — permisos
    /// granulares). Reutiliza Factory.CreateClient() tal cual (ningún nuevo tipo de infraestructura
    /// de test), y actualiza el ICurrentUser mutable compartido (MutableUser) para que
    /// IBranchAccessGuard resuelva la membresía/branch de ESTE usuario — el mismo mecanismo ya
    /// usado por SetActiveContext en las demás suites end-to-end.
    /// </summary>
    public HttpClient CreateClientForUser(Guid userId, string role)
    {
        _baseFactory.MutableUser.UserId = userId;
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestJwtFactory.CreateSessionJwt(TenantId, userId, role: role)
        );
        client.DefaultRequestHeaders.Add("X-Branch-Id", BranchId.ToString());
        return client;
    }

    /// <summary>Restaura el ICurrentUser mutable compartido al admin del fixture.</summary>
    public void RestoreAdminContext(Guid adminUserId) =>
        _baseFactory.MutableUser.UserId = adminUserId;

    public IServiceScope CreateDbScope() => Factory.Services.CreateScope();
}

[Trait("Category", "PostgreSql")]
public sealed class InventoryAdjustmentsEndToEndTests : IClassFixture<InventoryAdjustmentsFlowFixture>
{
    private readonly InventoryAdjustmentsFlowFixture _f;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public InventoryAdjustmentsEndToEndTests(InventoryAdjustmentsFlowFixture fixture) => _f = fixture;

    // ══════════════════════════════════════════════════════════════════════
    // Helpers privados
    // ══════════════════════════════════════════════════════════════════════

    private async Task<(Guid Id, string Code)> CreateReasonAsync(
        string allowedMovementType,
        bool requiresNotes = false,
        string? codePrefix = null,
        HttpClient? client = null
    )
    {
        var http = client ?? _f.Client;
        var code = $"{codePrefix ?? allowedMovementType.ToUpperInvariant()}-{Guid.NewGuid():N}"[..18];
        var response = await http.PostAsJsonAsync(
            "/api/v1/inventory/adjustment-reasons",
            new
            {
                companyId = (Guid?)null,
                code,
                name = $"Motivo {code}",
                allowedMovementType,
                requiresNotes,
                sortOrder = 1,
            }
        );
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var dto = (
            await response.Content.ReadFromJsonAsync<
                IaEnvelope<InventoryAdjustmentReasonResponseDto>
            >(JsonOptions)
        )!.Data!;
        return (dto.Id, dto.Code);
    }

    private async Task<HttpResponseMessage> CreateAdjustmentAsync(
        string movementType,
        Guid reasonId,
        string? notes,
        IEnumerable<object> lines,
        HttpClient? client = null
    ) =>
        await (client ?? _f.Client).PostAsJsonAsync(
            "/api/v1/inventory/stock/adjustments",
            new
            {
                warehouseId = _f.WarehouseId,
                warehouseName = "Bodega Principal",
                movementType,
                reasonId,
                notes,
                lines,
            }
        );

    private async Task<StockAdjustmentResponseDto> CreateAdjustmentOkAsync(
        string movementType,
        Guid reasonId,
        string? notes,
        IEnumerable<object> lines
    )
    {
        var response = await CreateAdjustmentAsync(movementType, reasonId, notes, lines);
        response
            .StatusCode.Should()
            .Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (
            await response.Content.ReadFromJsonAsync<IaEnvelope<StockAdjustmentResponseDto>>(
                JsonOptions
            )
        )!.Data!;
    }

    private async Task<HttpResponseMessage> ExecuteAdjustmentAsync(Guid id, HttpClient? client = null) =>
        await (client ?? _f.Client).PostAsync(
            $"/api/v1/inventory/stock/adjustments/{id}/execute",
            null
        );

    private async Task<HttpResponseMessage> CancelAdjustmentAsync(
        Guid id,
        string reason,
        HttpClient? client = null
    ) =>
        await (client ?? _f.Client).PostAsJsonAsync(
            $"/api/v1/inventory/stock/adjustments/{id}/cancel",
            new { reason }
        );

    private async Task<decimal> GetCurrentQuantityAsync(Guid itemId)
    {
        using var scope = _f.CreateDbScope();
        var stockRepo = scope.ServiceProvider.GetRequiredService<
            ERP.Domain.Modules.Inventory.Interfaces.IStockRepository
        >();
        var stock = await stockRepo.GetStockAsync(_f.TenantId, _f.WarehouseId, itemId, default);
        return stock?.Quantity ?? 0m;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ESCENARIO 1 — Motivos
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario1_Motivos_crear_actualizar_toggle_y_bloqueo_por_inactivo_y_RequiresNotes()
    {
        // ── Crear SOBRANTE (Ingreso) y CADUCADO (Egreso, RequiresNotes=true) ──
        var (sobranteId, sobranteCode) = await CreateReasonAsync(
            InventoryAdjustmentReason.Ingreso,
            requiresNotes: false,
            codePrefix: "SOBRANTE"
        );
        var (caducadoId, caducadoCode) = await CreateReasonAsync(
            InventoryAdjustmentReason.Egreso,
            requiresNotes: true,
            codePrefix: "CADUCADO"
        );

        var listResponse = await _f.Client.GetAsync("/api/v1/inventory/adjustment-reasons");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (
            await listResponse.Content.ReadFromJsonAsync<
                IaEnvelope<List<InventoryAdjustmentReasonResponseDto>>
            >(JsonOptions)
        )!.Data!;
        list.Should().Contain(r => r.Id == sobranteId && r.AllowedMovementType == "Ingreso");
        list.Should()
            .Contain(r => r.Id == caducadoId && r.AllowedMovementType == "Egreso" && r.RequiresNotes);

        // ── Update: Code es inmutable, Name/otros sí editables ──
        var updateResponse = await _f.Client.PutAsJsonAsync(
            $"/api/v1/inventory/adjustment-reasons/{sobranteId}",
            new
            {
                id = sobranteId,
                name = "Sobrante por conteo físico",
                allowedMovementType = InventoryAdjustmentReason.Ingreso,
                requiresNotes = false,
                sortOrder = 5,
            }
        );
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (
            await updateResponse.Content.ReadFromJsonAsync<
                IaEnvelope<InventoryAdjustmentReasonResponseDto>
            >(JsonOptions)
        )!.Data!;
        updated.Name.Should().Be("Sobrante por conteo físico");
        updated.Code.Should().Be(sobranteCode, "el Code es inmutable tras la creación");

        // ── Toggle SOBRANTE a inactivo ──
        var toggleResponse = await _f.Client.PostAsJsonAsync(
            $"/api/v1/inventory/adjustment-reasons/{sobranteId}/toggle",
            new { activate = false }
        );
        toggleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var toggled = (
            await toggleResponse.Content.ReadFromJsonAsync<
                IaEnvelope<InventoryAdjustmentReasonResponseDto>
            >(JsonOptions)
        )!.Data!;
        toggled.IsActive.Should().BeFalse();

        // ── Create de un ajuste con el motivo ya inactivo: el diseño -02 permite Create (el
        // catálogo se resuelve por existencia, no por IsActive) pero Execute lo rechaza ──
        var draftWithInactiveReason = await CreateAdjustmentOkAsync(
            InventoryAdjustmentReason.Ingreso,
            sobranteId,
            notes: null,
            lines: new object[]
            {
                new
                {
                    itemId = _f.ItemId,
                    itemName = "Producto Ajustes E2E",
                    packagingLevelId = (Guid?)null,
                    quantity = 1m,
                    unitCostBase = 5m,
                    lineNotes = (string?)null,
                },
            }
        );
        var executeWithInactiveReason = await ExecuteAdjustmentAsync(draftWithInactiveReason.Id);
        executeWithInactiveReason
            .StatusCode.Should()
            .Be(
                HttpStatusCode.UnprocessableEntity,
                await executeWithInactiveReason.Content.ReadAsStringAsync()
            );
        var inactiveReasonError = await executeWithInactiveReason.Content.ReadAsStringAsync();
        inactiveReasonError.Should().Contain("inactivo");

        // ── RequiresNotes=true (CADUCADO) bloquea Execute con Notes vacío, y permite con Notes ──
        var draftEmptyNotes = await CreateAdjustmentOkAsync(
            InventoryAdjustmentReason.Egreso,
            caducadoId,
            notes: null,
            lines: new object[]
            {
                new
                {
                    itemId = _f.ItemId,
                    itemName = "Producto Ajustes E2E",
                    packagingLevelId = (Guid?)null,
                    quantity = 1m,
                    unitCostBase = (decimal?)null,
                    lineNotes = (string?)null,
                },
            }
        );
        var executeEmptyNotes = await ExecuteAdjustmentAsync(draftEmptyNotes.Id);
        executeEmptyNotes
            .StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity, await executeEmptyNotes.Content.ReadAsStringAsync());
        var emptyNotesError = await executeEmptyNotes.Content.ReadAsStringAsync();
        emptyNotesError.Should().Contain("observaciones");

        // Nota: Egreso de 1 unidad requiere stock disponible — primero un Ingreso rápido con un
        // motivo Ingreso fresco para asegurar disponibilidad antes de reintentar el Execute.
        var (stockReasonId, _) = await CreateReasonAsync(
            InventoryAdjustmentReason.Ingreso,
            codePrefix: "PRESTOCK"
        );
        var ingresoPrevio = await CreateAdjustmentOkAsync(
            InventoryAdjustmentReason.Ingreso,
            stockReasonId,
            notes: null,
            lines: new object[]
            {
                new
                {
                    itemId = _f.ItemId,
                    itemName = "Producto Ajustes E2E",
                    packagingLevelId = (Guid?)null,
                    quantity = 5m,
                    unitCostBase = 1m,
                    lineNotes = (string?)null,
                },
            }
        );
        (await ExecuteAdjustmentAsync(ingresoPrevio.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var updateNotes = await _f.Client.PutAsJsonAsync(
            $"/api/v1/inventory/stock/adjustments/{draftEmptyNotes.Id}",
            new
            {
                id = draftEmptyNotes.Id,
                warehouseId = _f.WarehouseId,
                warehouseName = "Bodega Principal",
                movementType = InventoryAdjustmentReason.Egreso,
                reasonId = caducadoId,
                notes = "Vencido lote 123",
                lines = new object[]
                {
                    new
                    {
                        itemId = _f.ItemId,
                        itemName = "Producto Ajustes E2E",
                        packagingLevelId = (Guid?)null,
                        quantity = 1m,
                        unitCostBase = (decimal?)null,
                        lineNotes = (string?)null,
                    },
                },
            }
        );
        updateNotes.StatusCode.Should().Be(HttpStatusCode.OK, await updateNotes.Content.ReadAsStringAsync());

        var executeWithNotes = await ExecuteAdjustmentAsync(draftEmptyNotes.Id);
        executeWithNotes
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await executeWithNotes.Content.ReadAsStringAsync());
        var executed = (
            await executeWithNotes.Content.ReadFromJsonAsync<IaEnvelope<StockAdjustmentResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        executed.Status.Should().Be("Executed");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ESCENARIO 2 — Ingreso, unidad base
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario2_Ingreso_unidad_base_crea_Draft_sin_postear_y_Execute_postea_PositiveAdjust()
    {
        var (reasonId, _) = await CreateReasonAsync(InventoryAdjustmentReason.Ingreso, codePrefix: "SOBRANTE2");

        var qtyBeforeCreate = await GetCurrentQuantityAsync(_f.ItemId);

        var draft = await CreateAdjustmentOkAsync(
            InventoryAdjustmentReason.Ingreso,
            reasonId,
            notes: null,
            lines: new object[]
            {
                new
                {
                    itemId = _f.ItemId,
                    itemName = "Producto Ajustes E2E",
                    packagingLevelId = (Guid?)null,
                    quantity = 5m,
                    unitCostBase = 10.00m,
                    lineNotes = (string?)null,
                },
            }
        );
        draft.Status.Should().Be("Draft");

        // Draft NO postea StockMovement: el saldo actual no cambia por el solo hecho de crear.
        var getDraftResponse = await _f.Client.GetAsync(
            $"/api/v1/inventory/stock/adjustments/{draft.Id}"
        );
        getDraftResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getDraft = (
            await getDraftResponse.Content.ReadFromJsonAsync<IaEnvelope<StockAdjustmentResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        getDraft.Status.Should().Be("Draft");

        var qtyAfterCreate = await GetCurrentQuantityAsync(_f.ItemId);
        qtyAfterCreate.Should().Be(qtyBeforeCreate, "crear un Draft nunca debe tocar CurrentStock");

        using (var preScope = _f.CreateDbScope())
        {
            var preDb = preScope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var preMovementCount = await preDb.StockMovements.CountAsync(m =>
                m.SourceDocId == draft.Id
            );
            preMovementCount.Should().Be(0, "Draft no postea Kardex");
        }

        // ── Execute ──
        var executeResponse = await ExecuteAdjustmentAsync(draft.Id);
        executeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await executeResponse.Content.ReadAsStringAsync());
        var executed = (
            await executeResponse.Content.ReadFromJsonAsync<IaEnvelope<StockAdjustmentResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        executed.Status.Should().Be("Executed");

        var qtyAfterExecute = await GetCurrentQuantityAsync(_f.ItemId);
        qtyAfterExecute.Should().Be(qtyAfterCreate + 5m, "Execute debe incrementar el saldo en exactamente 5");

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var movement = await db.StockMovements.AsNoTracking().SingleAsync(m => m.SourceDocId == draft.Id);
        movement.MovementType.Should().Be(StockMovementType.PositiveAdjust);
        movement.Quantity.Should().Be(5m);
        movement.UomCode.Should().Be(_f.BaseUomCode);
        movement.SourceDocType.Should().Be("StockAdjustment");
        movement.SourceDocId.Should().Be(draft.Id);

        // El costo aplicado en un Ingreso es siempre el manual capturado en la línea (10.00) — el
        // Kardex nunca lo re-deriva. El RunningAverageCost resultante depende del estado previo del
        // ítem (puede no ser exactamente 10.00 si otro Fact ya dejó stock con costo distinto), así
        // que se calcula la expectativa con la MISMA fórmula que StockRepository.CreateAndTrackMovementAsync
        // (newValue = max(0, prevValue + qty*cost); newAvg = newValue/newQty) en vez de asumir "primer
        // movimiento".
        var stockBeforeThisMovement = await db
            .StockMovements.AsNoTracking()
            .Where(m =>
                m.ProductId == _f.ItemId && m.WarehouseId == _f.WarehouseId && m.Id != movement.Id
            )
            .OrderByDescending(m => m.SequenceNumber)
            .FirstOrDefaultAsync();
        var prevValue = stockBeforeThisMovement?.RunningStockValue ?? 0m;
        var prevQty = stockBeforeThisMovement?.ResultQuantity ?? 0m;
        var expectedNewValue = Math.Max(0m, prevValue + 5m * 10.00m);
        var expectedNewQty = prevQty + 5m;
        var expectedAvg = expectedNewQty > 0m ? expectedNewValue / expectedNewQty : 0m;
        // running_average_cost es numeric(18,6) en BD — redondear la expectativa a la misma
        // precisión antes de comparar evita diffs espurios de redondeo (p. ej.
        // 6.1818181818...M calculado en memoria vs 6.181818M ya redondeado al persistir).
        movement
            .RunningAverageCost.Should()
            .Be(Math.Round(expectedAvg, 6, MidpointRounding.AwayFromZero));

        var line = executed.Lines.Single();
        line.UnitCostBase.Should().Be(10.00m);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ESCENARIO 3 — Egreso, unidad base
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario3_Egreso_unidad_base_consume_costo_promedio_corrido_sin_costo_manual()
    {
        var (ingresoReasonId, _) = await CreateReasonAsync(
            InventoryAdjustmentReason.Ingreso,
            codePrefix: "SOBRANTE3"
        );
        var (egresoReasonId, _) = await CreateReasonAsync(
            InventoryAdjustmentReason.Egreso,
            requiresNotes: true,
            codePrefix: "CADUCADO3"
        );

        // Asegura stock suficiente propio de este test (independiente del orden de ejecución).
        var ingreso = await CreateAdjustmentOkAsync(
            InventoryAdjustmentReason.Ingreso,
            ingresoReasonId,
            notes: null,
            lines: new object[]
            {
                new
                {
                    itemId = _f.ItemId,
                    itemName = "Producto Ajustes E2E",
                    packagingLevelId = (Guid?)null,
                    quantity = 5m,
                    unitCostBase = 10.00m,
                    lineNotes = (string?)null,
                },
            }
        );
        (await ExecuteAdjustmentAsync(ingreso.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var qtyBeforeEgreso = await GetCurrentQuantityAsync(_f.ItemId);
        decimal avgCostBeforeEgreso;
        using (var preScope = _f.CreateDbScope())
        {
            var preDb = preScope.ServiceProvider.GetRequiredService<ErpDbContext>();
            // Se lee el RunningAverageCost del ÚLTIMO StockMovement (columna numeric(18,6), ya
            // redondeada al guardar) en vez de CurrentStock.AverageCost (TotalStockValue/Quantity
            // calculado en memoria SIN redondeo) — comparar contra este último produce diffs de
            // redondeo espurios (p. ej. 6.1818181818... vs 6.181818) que no reflejan ningún error
            // real: AppendMovementAsync consume exactamente el RunningAverageCost del movimiento
            // anterior, nunca CurrentStock.AverageCost (prohibido para costeo — ver comentario en
            // StockRepository.CreateAndTrackMovementAsync).
            var lastMovement = await preDb
                .StockMovements.AsNoTracking()
                .Where(m => m.ProductId == _f.ItemId && m.WarehouseId == _f.WarehouseId)
                .OrderByDescending(m => m.SequenceNumber)
                .FirstAsync();
            avgCostBeforeEgreso = lastMovement.RunningAverageCost;
        }

        // La API acepta unitCostBase: null en una línea de Egreso — el validator solo exige
        // GreaterThanOrEqualTo(0) CUANDO tiene valor (CreateStockAdjustmentValidator), y
        // ExecuteStockAdjustmentCommandHandler explícitamente NO pasa unitCost para Egreso
        // (deja que AppendMovementAsync resuelva el costo promedio corrido).
        var egresoDraft = await CreateAdjustmentOkAsync(
            InventoryAdjustmentReason.Egreso,
            egresoReasonId,
            notes: "Vencido lote 123",
            lines: new object[]
            {
                new
                {
                    itemId = _f.ItemId,
                    itemName = "Producto Ajustes E2E",
                    packagingLevelId = (Guid?)null,
                    quantity = 2m,
                    unitCostBase = (decimal?)null,
                    lineNotes = (string?)null,
                },
            }
        );

        var executeResponse = await ExecuteAdjustmentAsync(egresoDraft.Id);
        executeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await executeResponse.Content.ReadAsStringAsync());

        var qtyAfterEgreso = await GetCurrentQuantityAsync(_f.ItemId);
        qtyAfterEgreso.Should().Be(qtyBeforeEgreso - 2m);

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var movement = await db
            .StockMovements.AsNoTracking()
            .SingleAsync(m => m.SourceDocId == egresoDraft.Id);
        movement.MovementType.Should().Be(StockMovementType.NegativeAdjust);
        movement.Quantity.Should().Be(-2m);
        // Egreso nunca captura un costo manual en el movimiento (UnitCost queda null — solo se
        // pasa unitCost explícito para Ingreso, ver ExecuteStockAdjustmentCommandHandler línea
        // "unitCost: isIngreso ? line.UnitCostBase : null"). El costo SÍ se resuelve internamente
        // a partir del promedio corrido — verificable por RunningAverageCost, que para un Egreso
        // (que solo reduce cantidad y valor en la misma proporción) debe permanecer igual al
        // promedio corrido inmediatamente anterior.
        movement.UnitCost.Should().BeNull("un Egreso nunca captura un costo manual en el Kardex");
        movement
            .RunningAverageCost.Should()
            .Be(
                avgCostBeforeEgreso,
                "el costo consumido por un Egreso debe ser el promedio corrido previo — un Egreso no cambia el costo promedio, solo la cantidad/valor"
            );
    }

    // ══════════════════════════════════════════════════════════════════════
    // ESCENARIO 4 — Stock insuficiente
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario4_Egreso_con_stock_insuficiente_Create_permite_pero_Execute_rechaza()
    {
        var (egresoReasonId, _) = await CreateReasonAsync(
            InventoryAdjustmentReason.Egreso,
            codePrefix: "CADUCADO4"
        );

        var currentQty = await GetCurrentQuantityAsync(_f.ItemId);
        var excessiveQty = currentQty + 1_000_000m;

        var draft = await CreateAdjustmentOkAsync(
            InventoryAdjustmentReason.Egreso,
            egresoReasonId,
            notes: null,
            lines: new object[]
            {
                new
                {
                    itemId = _f.ItemId,
                    itemName = "Producto Ajustes E2E",
                    packagingLevelId = (Guid?)null,
                    quantity = excessiveQty,
                    unitCostBase = (decimal?)null,
                    lineNotes = (string?)null,
                },
            }
        );
        draft.Status.Should().Be("Draft", "Draft nunca valida stock — solo Execute lo hace");

        int movementCountBefore;
        using (var preScope = _f.CreateDbScope())
        {
            var preDb = preScope.ServiceProvider.GetRequiredService<ErpDbContext>();
            movementCountBefore = await preDb.StockMovements.CountAsync(m => m.SourceDocId == draft.Id);
        }
        movementCountBefore.Should().Be(0);

        var executeResponse = await ExecuteAdjustmentAsync(draft.Id);
        executeResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await executeResponse.Content.ReadAsStringAsync();
        body.Should()
            .Contain("Stock insuficiente", "el mensaje de error debe ser específico, no genérico");
        body.Should().Contain(excessiveQty.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var movementCountAfter = await db.StockMovements.CountAsync(m => m.SourceDocId == draft.Id);
        movementCountAfter.Should().Be(0, "un Execute fallido no debe dejar ningún StockMovement");

        var qtyAfterFailedExecute = await GetCurrentQuantityAsync(_f.ItemId);
        qtyAfterFailedExecute.Should().Be(currentQty, "un Execute fallido no debe mutar CurrentStock");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ESCENARIO 5 — Presentación (Caja x12)
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario5_Ingreso_con_PackagingLevel_Caja_x12_resuelve_QuantityInBaseUom_correctamente()
    {
        var (reasonId, _) = await CreateReasonAsync(InventoryAdjustmentReason.Ingreso, codePrefix: "SOBRANTE5");

        var qtyBefore = await GetCurrentQuantityAsync(_f.ItemId);

        var draft = await CreateAdjustmentOkAsync(
            InventoryAdjustmentReason.Ingreso,
            reasonId,
            notes: null,
            lines: new object[]
            {
                new
                {
                    itemId = _f.ItemId,
                    itemName = "Producto Ajustes E2E",
                    packagingLevelId = _f.BoxPackagingLevelId,
                    quantity = 1m,
                    unitCostBase = 3m,
                    lineNotes = (string?)null,
                },
            }
        );
        var draftLine = draft.Lines.Single();
        draftLine.ConversionFactor.Should().Be(_f.BoxConversionFactor);
        draftLine.QuantityInBaseUom.Should().Be(_f.BoxConversionFactor);
        draftLine.UomCode.Should().Be("BOX");
        draftLine.BaseUomCode.Should().Be(_f.BaseUomCode);

        var executeResponse = await ExecuteAdjustmentAsync(draft.Id);
        executeResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await executeResponse.Content.ReadAsStringAsync());
        var executed = (
            await executeResponse.Content.ReadFromJsonAsync<IaEnvelope<StockAdjustmentResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        var executedLine = executed.Lines.Single();
        executedLine.ConversionFactor.Should().Be(_f.BoxConversionFactor);
        executedLine.QuantityInBaseUom.Should().Be(_f.BoxConversionFactor);

        var qtyAfter = await GetCurrentQuantityAsync(_f.ItemId);
        qtyAfter.Should().Be(qtyBefore + _f.BoxConversionFactor, "1 caja x12 debe posteer 12 unidades base, no 1");

        using var scope = _f.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var movement = await db.StockMovements.AsNoTracking().SingleAsync(m => m.SourceDocId == draft.Id);
        movement.Quantity.Should().Be(_f.BoxConversionFactor);
        movement.UomCode.Should().Be(_f.BaseUomCode, "el Kardex siempre postea en unidad base, nunca en la presentación de captura");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ESCENARIO 6 — Anulación
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario6_Cancel_postea_movimientos_inversos_sin_tocar_los_originales_y_neto_es_cero()
    {
        var (reasonId, _) = await CreateReasonAsync(InventoryAdjustmentReason.Ingreso, codePrefix: "SOBRANTE6");

        var qtyBefore = await GetCurrentQuantityAsync(_f.ItemId);

        var draft = await CreateAdjustmentOkAsync(
            InventoryAdjustmentReason.Ingreso,
            reasonId,
            notes: null,
            lines: new object[]
            {
                new
                {
                    itemId = _f.ItemId,
                    itemName = "Producto Ajustes E2E",
                    packagingLevelId = (Guid?)null,
                    quantity = 7m,
                    unitCostBase = 4m,
                    lineNotes = (string?)null,
                },
            }
        );
        (await ExecuteAdjustmentAsync(draft.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var qtyAfterExecute = await GetCurrentQuantityAsync(_f.ItemId);
        qtyAfterExecute.Should().Be(qtyBefore + 7m);

        // Snapshot del/los StockMovement original(es) ANTES de anular.
        Guid originalMovementId;
        StockMovementType originalType;
        decimal originalQuantity;
        decimal originalResultQuantity;
        decimal originalRunningAverageCost;
        decimal originalRunningStockValue;
        using (var preScope = _f.CreateDbScope())
        {
            var preDb = preScope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var original = await preDb
                .StockMovements.AsNoTracking()
                .SingleAsync(m => m.SourceDocId == draft.Id);
            originalMovementId = original.Id;
            originalType = original.MovementType;
            originalQuantity = original.Quantity;
            originalResultQuantity = original.ResultQuantity;
            originalRunningAverageCost = original.RunningAverageCost;
            originalRunningStockValue = original.RunningStockValue;
        }

        var cancelResponse = await CancelAdjustmentAsync(draft.Id, "Conteo erróneo — se revierte");
        cancelResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, await cancelResponse.Content.ReadAsStringAsync());
        var cancelled = (
            await cancelResponse.Content.ReadFromJsonAsync<IaEnvelope<StockAdjustmentResponseDto>>(
                JsonOptions
            )
        )!.Data!;
        cancelled.Status.Should().Be("Cancelled");
        cancelled.CancelledAt.Should().NotBeNull();
        cancelled.CancelledBy.Should().NotBeNull();
        cancelled.CancelledReason.Should().Be("Conteo erróneo — se revierte");

        using (var scope = _f.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

            // El movimiento ORIGINAL sigue existiendo, sin cambios.
            var original = await db.StockMovements.AsNoTracking().SingleAsync(m => m.Id == originalMovementId);
            original.MovementType.Should().Be(originalType);
            original.Quantity.Should().Be(originalQuantity);
            original.ResultQuantity.Should().Be(originalResultQuantity);
            original.RunningAverageCost.Should().Be(originalRunningAverageCost);
            original.RunningStockValue.Should().Be(originalRunningStockValue);

            // Existe un movimiento INVERSO nuevo, mismo SourceDocId/Type, signo opuesto.
            var allMovements = await db
                .StockMovements.AsNoTracking()
                .Where(m => m.SourceDocId == draft.Id && m.SourceDocType == "StockAdjustment")
                .ToListAsync();
            allMovements.Should().HaveCount(2, "el original + su reversa");
            var reversal = allMovements.Single(m => m.Id != originalMovementId);
            reversal.MovementType.Should().Be(StockMovementType.NegativeAdjust);
            reversal.Quantity.Should().Be(-7m);
            reversal.Reference.Should().Contain("ANULACIÓN");
            reversal.Reference.Should().Contain(draft.AdjustmentNumber);
        }

        var qtyAfterCancel = await GetCurrentQuantityAsync(_f.ItemId);
        qtyAfterCancel.Should().Be(qtyBefore, "Execute + Cancel debe tener efecto neto cero sobre el saldo");

        // Cancelar de nuevo debe rechazarse (no "resucita").
        var secondCancel = await CancelAdjustmentAsync(draft.Id, "Segundo intento");
        secondCancel.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ESCENARIO 7 — Bloqueos (estado + permisos)
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Escenario7a_Update_sobre_ajuste_Executed_es_rechazado()
    {
        var (reasonId, _) = await CreateReasonAsync(InventoryAdjustmentReason.Ingreso, codePrefix: "SOBRANTE7A");

        var draft = await CreateAdjustmentOkAsync(
            InventoryAdjustmentReason.Ingreso,
            reasonId,
            notes: null,
            lines: new object[]
            {
                new
                {
                    itemId = _f.ItemId,
                    itemName = "Producto Ajustes E2E",
                    packagingLevelId = (Guid?)null,
                    quantity = 1m,
                    unitCostBase = 1m,
                    lineNotes = (string?)null,
                },
            }
        );
        (await ExecuteAdjustmentAsync(draft.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var updateResponse = await _f.Client.PutAsJsonAsync(
            $"/api/v1/inventory/stock/adjustments/{draft.Id}",
            new
            {
                id = draft.Id,
                warehouseId = _f.WarehouseId,
                warehouseName = "Bodega Principal",
                movementType = InventoryAdjustmentReason.Ingreso,
                reasonId,
                notes = "intento de editar un ejecutado",
                lines = new object[]
                {
                    new
                    {
                        itemId = _f.ItemId,
                        itemName = "Producto Ajustes E2E",
                        packagingLevelId = (Guid?)null,
                        quantity = 99m,
                        unitCostBase = 1m,
                        lineNotes = (string?)null,
                    },
                },
            }
        );
        updateResponse
            .StatusCode.Should()
            .Be(HttpStatusCode.UnprocessableEntity, await updateResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Escenario7b_Execute_sobre_ajuste_ya_Cancelled_es_rechazado()
    {
        var (reasonId, _) = await CreateReasonAsync(InventoryAdjustmentReason.Ingreso, codePrefix: "SOBRANTE7B");

        var draft = await CreateAdjustmentOkAsync(
            InventoryAdjustmentReason.Ingreso,
            reasonId,
            notes: null,
            lines: new object[]
            {
                new
                {
                    itemId = _f.ItemId,
                    itemName = "Producto Ajustes E2E",
                    packagingLevelId = (Guid?)null,
                    quantity = 1m,
                    unitCostBase = 1m,
                    lineNotes = (string?)null,
                },
            }
        );
        (await ExecuteAdjustmentAsync(draft.Id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await CancelAdjustmentAsync(draft.Id, "Anulado para el test")).StatusCode.Should()
            .Be(HttpStatusCode.OK);

        var executeAfterCancel = await ExecuteAdjustmentAsync(draft.Id);
        executeAfterCancel.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// Verifica el pipeline REAL de autorización granular (no solo el bypass de Admin, ya cubierto
    /// implícitamente por todos los demás escenarios). Construye un AccessProfile con únicamente
    /// "inventory.adjustments.view" y prueba que ese usuario recibe 403 al intentar Create (que
    /// requiere "inventory.adjustments.create"); y un segundo perfil con view+create pero SIN
    /// "inventory.adjustments.confirm" y prueba 403 en Execute. Esto SÍ ejercita
    /// RuntimePermissionAuthorizer/PermissionHandler/EffectivePermissionKeysProvider reales contra
    /// PostgreSQL — no es un mecanismo de test nuevo, reutiliza AccessProfile/AccessProfilePermission/
    /// CompanyUserMembership.ProfileId, que ya existen en el dominio de Access, más
    /// Factory.CreateClient() para el segundo cliente HTTP (mismo patrón ya usado por el fixture).
    /// </summary>
    [Fact]
    public async Task Escenario7c_Usuario_sin_permiso_de_Create_o_Confirm_recibe_403()
    {
        // ── Usuario con SOLO permiso de vista: Create debe ser 403 ──
        var viewOnlyProfileId = await _f.CreateProfileWithPermissionsAsync(
            "Solo Vista Ajustes",
            "inventory.adjustments.view",
            "inventory.adjustment-reasons.view"
        );
        var viewOnlyUserId = await _f.CreateUserWithBranchAccessAsync("Operador", viewOnlyProfileId);
        var viewOnlyClient = _f.CreateClientForUser(viewOnlyUserId, "Operador");

        var (reasonId, _) = await CreateReasonAsync(
            InventoryAdjustmentReason.Ingreso,
            codePrefix: "SOBRANTE7C"
        );

        var createAsViewOnly = await CreateAdjustmentAsync(
            InventoryAdjustmentReason.Ingreso,
            reasonId,
            notes: null,
            lines: new object[]
            {
                new
                {
                    itemId = _f.ItemId,
                    itemName = "Producto Ajustes E2E",
                    packagingLevelId = (Guid?)null,
                    quantity = 1m,
                    unitCostBase = 1m,
                    lineNotes = (string?)null,
                },
            },
            client: viewOnlyClient
        );
        createAsViewOnly.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // La lectura (view) sí debe funcionar para este usuario.
        var listAsViewOnly = await viewOnlyClient.GetAsync("/api/v1/inventory/adjustment-reasons");
        listAsViewOnly.StatusCode.Should().Be(HttpStatusCode.OK);

        // ── Usuario con vista+creación pero SIN confirm: Execute debe ser 403 ──
        var createOnlyProfileId = await _f.CreateProfileWithPermissionsAsync(
            "Vista y Creación Ajustes",
            "inventory.adjustments.view",
            "inventory.adjustments.create",
            "inventory.adjustment-reasons.view"
        );
        var createOnlyUserId = await _f.CreateUserWithBranchAccessAsync("Operador", createOnlyProfileId);
        var createOnlyClient = _f.CreateClientForUser(createOnlyUserId, "Operador");

        var draftByCreateOnlyUser = await CreateAdjustmentAsync(
            InventoryAdjustmentReason.Ingreso,
            reasonId,
            notes: null,
            lines: new object[]
            {
                new
                {
                    itemId = _f.ItemId,
                    itemName = "Producto Ajustes E2E",
                    packagingLevelId = (Guid?)null,
                    quantity = 1m,
                    unitCostBase = 1m,
                    lineNotes = (string?)null,
                },
            },
            client: createOnlyClient
        );
        draftByCreateOnlyUser
            .StatusCode.Should()
            .Be(HttpStatusCode.Created, await draftByCreateOnlyUser.Content.ReadAsStringAsync());
        var draft = (
            await draftByCreateOnlyUser.Content.ReadFromJsonAsync<
                IaEnvelope<StockAdjustmentResponseDto>
            >(JsonOptions)
        )!.Data!;

        var executeByCreateOnlyUser = await ExecuteAdjustmentAsync(draft.Id, createOnlyClient);
        executeByCreateOnlyUser.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Restaura el contexto de usuario mutable compartido al admin del fixture para no afectar
        // otros Facts que puedan ejecutarse después usando _f.Client.
        var adminRestoreUserId = await _f.CreateUserWithBranchAccessAsync("Admin", null);
        _f.RestoreAdminContext(adminRestoreUserId);
    }
}

// ══════════════════════════════════════════════════════════════════════════
// DTOs locales — mismo criterio que las demás suites end-to-end: no se
// reutilizan contratos de otras capas, se deserializa solo lo que este
// archivo necesita.
// ══════════════════════════════════════════════════════════════════════════

internal sealed record IaEnvelope<T>(T? Data);

internal sealed record InventoryAdjustmentReasonResponseDto(
    Guid Id,
    string Code,
    string Name,
    string AllowedMovementType,
    bool RequiresNotes,
    bool IsActive,
    int SortOrder
);

internal sealed record StockAdjustmentLineResponseDto(
    Guid Id,
    Guid ItemId,
    string ItemName,
    Guid? PackagingLevelId,
    string UomCode,
    string BaseUomCode,
    decimal ConversionFactor,
    decimal Quantity,
    decimal QuantityInBaseUom,
    decimal? UnitCostBase,
    decimal? TotalCost,
    decimal? CurrentStockBefore,
    decimal? CurrentStockAfter,
    string? LineNotes
);

internal sealed record StockAdjustmentResponseDto(
    Guid Id,
    string AdjustmentNumber,
    Guid WarehouseId,
    string WarehouseName,
    string MovementType,
    Guid ReasonId,
    string? ReasonName,
    string? Notes,
    DateTime AdjustmentDate,
    string Status,
    DateTime? ExecutedAt,
    Guid? ExecutedBy,
    DateTime? CancelledAt,
    Guid? CancelledBy,
    string? CancelledReason,
    List<StockAdjustmentLineResponseDto> Lines
);
