using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// P0-02 Fase 6 — Remediación 02. Cubre el punto 1 de la revisión fallida (los 7 escenarios de
/// <c>PurchaseReturnSequence.CaptureNextAsync</c> integrados al <b>handler real</b>
/// <see cref="AuthorizePurchaseReturnHandler"/>, no la secuencia aislada — la Fase 2 ya cubre la
/// secuencia aislada) y el punto 7 (ausencia de doble numeración por consulta directa contra
/// PostgreSQL real, sin apoyarse únicamente en el <c>Result</c> del handler).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class AuthorizePurchaseReturnSequenceConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_authorize_return_sequence_concurrency_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _warehouseId;
    private Guid _supplierId;
    private Guid _paymentTermId;
    private Guid _itemId;
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;

        var branch = Branch.Create(
            tenantId: _tenantId,
            name: "Matriz",
            address: "Av. Principal 123",
            code: "B01",
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
            createdBy: _userId,
            companyId: _companyId
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        _branchId = branch.Id;

        var supplier = BusinessPartner.Create(
            _tenantId,
            "05",
            "1710034065",
            1,
            "Proveedor Test",
            _userId
        );
        var paymentTerm = PaymentTerm.Create(
            _tenantId,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _userId
        );
        var warehouse = ERP.Domain.Modules.Inventory.Entities.Warehouse.Create(
            _tenantId,
            _branchId,
            "Bodega Principal",
            "BOD-01",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _userId,
            _companyId,
            isMain: true
        );
        db.BusinessPartners.Add(supplier);
        db.Add(paymentTerm);
        db.Add(warehouse);
        await db.SaveChangesAsync();
        _supplierId = supplier.Id;
        _paymentTermId = paymentTerm.Id;
        _warehouseId = warehouse.Id;

        var itemType = ItemTypeDefinition.Create(_tenantId, "MERCH", "Mercadería", 1, _userId);
        db.Set<ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        // Un solo Item compartido entre facturas — cada factura obtiene su propio lote de stock,
        // así ninguna línea de estos tests colisiona con las de otro escenario.
        var item = Item.Create(
            _tenantId,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto Devolución Test",
            description: "Producto Devolución Test",
            itemTypeId: itemType.Id,
            defaultUomCode: "UNIT",
            taxConfig: ItemTaxConfig.Create(saleVatCode: "10", purchaseVatCode: "10"),
            saleConfig: ItemSaleConfig.Create(isForSale: true),
            stockConfig: ItemStockConfig.Create(tracksStock: true),
            createdBy: _userId
        );
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();
        _itemId = item.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new ErpDbContext(
            options,
            new FixedCurrentTenant(() => _tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(() => _companyId)
        );
    }

    /// <summary>Factura confirmada + CxP + stock suficiente + devolución en Draft lista para autorizar — cada llamada crea una factura/devolución completamente independiente.</summary>
    private async Task<Guid> SeedAuthorizableDraftAsync(
        decimal lineQuantity = 10,
        decimal returnQuantity = 2
    )
    {
        await using var db = CreateContext();
        var inv = Domain.Modules.Purchases.Entities.PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999)}",
            DateOnly.FromDateTime(DateTime.UtcNow),
            _userId,
            _paymentTermId,
            "Contado",
            1,
            30,
            globalWarehouseId: _warehouseId
        );
        var line = Domain.Modules.Purchases.Entities.PurchaseInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto 1",
            quantity: lineQuantity,
            unitPrice: 10.00m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: _itemId,
            warehouseId: _warehouseId
        );
        line.ApplyTaxes("10", 10m, "IVA", null, 0m, null);
        inv.ReplaceLines(new[] { line }, _userId);
        inv.Confirm(_userId);
        db.PurchaseInvoices.Add(inv);

        var payable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            inv.Id,
            "01",
            inv.InvoiceNumber,
            inv.IssueDate,
            inv.IssueDate,
            _userId
        );
        payable.AddInstallment(1, inv.IssueDate.AddDays(30), inv.GrandTotal);
        db.Set<AccountsPayable>().Add(payable);
        await db.SaveChangesAsync();

        var stockRepo = new StockRepository(
            db,
            new FixedCurrentCompany(() => _companyId),
            new RealDatabaseExceptionTranslator()
        );
        await stockRepo.AppendMovementAsync(
            _tenantId,
            _companyId,
            _itemId,
            _warehouseId,
            ERP.Domain.Modules.Inventory.Enums.StockMovementType.PurchaseEntry,
            lineQuantity,
            "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Ingreso inicial",
            inv.Id,
            "PurchaseInvoice",
            _userId,
            unitCost: 10.00m,
            ct: CancellationToken.None
        );
        await stockRepo.SaveChangesWithSequenceRetryAsync();

        var returnRepo = new PurchaseReturnRepository(
            db,
            new FixedCurrentCompany(() => _companyId)
        );
        var invoiceRepo = new PurchaseInvoiceRepository(
            db,
            new FixedCurrentCompany(() => _companyId)
        );
        var createHandler = new CreatePurchaseReturnDraftHandler(
            returnRepo,
            invoiceRepo,
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentBranch(() => _branchId),
            new FixedCurrentUser(_userId)
        );

        var invoice = await invoiceRepo.GetByIdAsync(_tenantId, inv.Id, CancellationToken.None);
        var lineId = invoice!.Lines.Single().Id;

        var createResult = await createHandler.Handle(
            new CreatePurchaseReturnDraftCommand(
                Guid.NewGuid(),
                inv.Id,
                "Producto en mal estado",
                new[] { new PurchaseReturnDraftLineInput(lineId, returnQuantity) }
            ),
            CancellationToken.None
        );

        return createResult.Value!.Id;
    }

    private async Task<(bool Success, PurchaseReturn? Value)> ExecuteAuthorizeAsync(
        Guid purchaseReturnId,
        Guid clientRequestId
    )
    {
        await using var db = CreateContext();
        var returnRepo = new PurchaseReturnRepository(
            db,
            new FixedCurrentCompany(() => _companyId)
        );
        var invoiceRepo = new PurchaseInvoiceRepository(
            db,
            new FixedCurrentCompany(() => _companyId)
        );
        var sequenceRepo = new PurchaseReturnSequenceRepository(db);
        var stockRepo = new StockRepository(
            db,
            new FixedCurrentCompany(() => _companyId),
            new RealDatabaseExceptionTranslator()
        );
        var creditRepo = new SupplierCreditRepository(
            db,
            new FixedCurrentCompany(() => _companyId)
        );
        var uow = new UnitOfWork(db);

        var handler = new AuthorizePurchaseReturnHandler(
            returnRepo,
            invoiceRepo,
            new AccountsPayableRepository(db),
            sequenceRepo,
            stockRepo,
            creditRepo,
            uow,
            new RealDatabaseExceptionTranslator(),
            Mock.Of<IPostingEngine>(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

        var result = await handler.Handle(
            new AuthorizePurchaseReturnCommand(purchaseReturnId, clientRequestId),
            CancellationToken.None
        );

        if (!result.IsSuccess)
            return (false, null);

        await using var verify = CreateContext();
        var reloaded = await returnRepo.GetByIdAsync(
            _tenantId,
            purchaseReturnId,
            CancellationToken.None
        );
        return (true, reloaded);
    }

    // ── 1. Autorización simple captura número ────────────────────────────

    [Fact]
    public async Task Punto1_1_Autorizacion_simple_captura_un_numero_valido_D8()
    {
        var purchaseReturnId = await SeedAuthorizableDraftAsync();

        var result = await ExecuteAuthorizeAsync(purchaseReturnId, Guid.NewGuid());

        result.Success.Should().BeTrue();
        result.Value!.ReturnNumber.Should().NotBeNullOrWhiteSpace();
        result.Value.ReturnNumber!.Length.Should().Be(8);
    }

    // ── 2. Dos autorizaciones concurrentes capturan números distintos ────

    [Fact]
    public async Task Punto1_2_Dos_autorizaciones_concurrentes_sobre_facturas_distintas_capturan_numeros_distintos()
    {
        var returnId1 = await SeedAuthorizableDraftAsync();
        var returnId2 = await SeedAuthorizableDraftAsync();

        var t1 = ExecuteAuthorizeAsync(returnId1, Guid.NewGuid());
        var t2 = ExecuteAuthorizeAsync(returnId2, Guid.NewGuid());
        var results = await Task.WhenAll(t1, t2);

        results.Should().OnlyContain(r => r.Success);
        results[0].Value!.ReturnNumber.Should().NotBe(results[1].Value!.ReturnNumber);
    }

    // ── 3. No hay saltos indebidos por retry ──────────────────────────────

    [Fact]
    public async Task Punto1_3_Autorizaciones_sucesivas_sobre_facturas_distintas_producen_numeracion_contigua_sin_huecos()
    {
        // CaptureNextAsync usa pg_advisory_xact_lock de ámbito transaccional (§7.1bis): el
        // incremento de CurrentSeq viaja en la MISMA transacción que el resto de Authorize — si
        // la transacción se revierte por cualquier motivo, el incremento nunca se persiste. Por
        // construcción no puede quedar un número "perdido" (hueco) por un intento fallido —
        // se verifica aquí de forma end-to-end: 3 autorizaciones exitosas consecutivas producen
        // números estrictamente consecutivos, sin saltos.
        var returnIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
            returnIds.Add(await SeedAuthorizableDraftAsync());

        var numbers = new List<int>();
        foreach (var id in returnIds)
        {
            var result = await ExecuteAuthorizeAsync(id, Guid.NewGuid());
            result.Success.Should().BeTrue();
            numbers.Add(int.Parse(result.Value!.ReturnNumber!));
        }

        for (var i = 1; i < numbers.Count; i++)
            numbers[i].Should().Be(numbers[i - 1] + 1, "no debe haber huecos en la numeración");
    }

    // ── 4. No hay duplicidad de número ────────────────────────────────────

    [Fact]
    public async Task Punto1_4_Cinco_autorizaciones_concurrentes_sobre_facturas_distintas_no_producen_numeros_duplicados()
    {
        var returnIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
            returnIds.Add(await SeedAuthorizableDraftAsync());

        var tasks = returnIds.Select(id => ExecuteAuthorizeAsync(id, Guid.NewGuid())).ToArray();
        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(r => r.Success);
        var numbers = results.Select(r => r.Value!.ReturnNumber).ToList();
        numbers.Should().OnlyHaveUniqueItems();
    }

    // ── 5. Conflicto de secuencia se resuelve con SaveChangesWithSequenceRetryAsync ──

    [Fact]
    public async Task Punto1_5_Autorizaciones_concurrentes_que_comparten_Item_y_bodega_en_facturas_distintas_resuelven_conflicto_via_retry()
    {
        // Dos facturas distintas (Lock A independiente, NO serializadas entre sí por
        // PurchaseInvoiceId) devolviendo el MISMO Item en la MISMA bodega generan una
        // colisión real de StockMovement.SequenceNumber (clave Company/Product/Warehouse) al
        // autorizar en paralelo — exactamente el escenario que
        // StockRepository.SaveChangesWithSequenceRetryAsync (§16.3) resuelve reintentando la
        // recomputación desde el estado real de la base. Aquí se verifica integrado al handler
        // completo de Authorize, no de forma aislada.
        var returnId1 = await SeedAuthorizableDraftAsync();
        var returnId2 = await SeedAuthorizableDraftAsync();

        var t1 = ExecuteAuthorizeAsync(returnId1, Guid.NewGuid());
        var t2 = ExecuteAuthorizeAsync(returnId2, Guid.NewGuid());
        var results = await Task.WhenAll(t1, t2);

        results
            .Should()
            .OnlyContain(
                r => r.Success,
                "SaveChangesWithSequenceRetryAsync debe absorber el conflicto de secuencia sin que ninguna autorización falle"
            );

        await using var verify = CreateContext();
        var movements = await verify
            .Set<ERP.Domain.Modules.Inventory.Entities.StockMovement>()
            .Where(m =>
                m.TenantId == _tenantId && m.ProductId == _itemId && m.WarehouseId == _warehouseId
            )
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync();

        movements.Select(m => m.SequenceNumber).Should().OnlyHaveUniqueItems();
    }

    // ── 6. Numeración queda persistida después de commit ─────────────────

    [Fact]
    public async Task Punto1_6_Numero_capturado_queda_persistido_tras_commit_visible_desde_otro_contexto()
    {
        var purchaseReturnId = await SeedAuthorizableDraftAsync();
        var result = await ExecuteAuthorizeAsync(purchaseReturnId, Guid.NewGuid());
        result.Success.Should().BeTrue();

        await using var verify = CreateContext();
        var persisted = await verify
            .PurchaseReturns.AsNoTracking()
            .FirstAsync(r => r.Id == purchaseReturnId);
        persisted.ReturnNumber.Should().Be(result.Value!.ReturnNumber);

        // PurchaseReturnSequence.CurrentSeq representa el PRÓXIMO número a emitir (no el último
        // emitido) — tras capturar "00000001", CurrentSeq avanza a 2 en memoria y así queda
        // persistido (PurchaseReturnSequence.CaptureAndIncrement: retorna el valor actual
        // formateado y LUEGO incrementa). Verificación correcta: CurrentSeq - 1 == número emitido.
        var sequence = await verify
            .PurchaseReturnSequences.AsNoTracking()
            .FirstAsync(s => s.TenantId == _tenantId && s.CompanyId == _companyId);
        (sequence.CurrentSeq - 1).ToString("D8").Should().Be(result.Value.ReturnNumber);
    }

    // ── 7. Consulta directa confirma ausencia de doble numeración ────────

    [Fact]
    public async Task Punto1_7_Consulta_directa_tras_lote_concurrente_confirma_ausencia_de_doble_numeracion()
    {
        var returnIds = new List<Guid>();
        for (var i = 0; i < 6; i++)
            returnIds.Add(await SeedAuthorizableDraftAsync());

        var tasks = returnIds.Select(id => ExecuteAuthorizeAsync(id, Guid.NewGuid())).ToArray();
        var results = await Task.WhenAll(tasks);
        results.Should().OnlyContain(r => r.Success);

        // Verificación por consulta SQL directa — no basta con el Result del handler (regla
        // explícita de la remediación): agrupar por ReturnNumber y confirmar que ningún grupo
        // tiene más de una fila.
        await using var verify = CreateContext();
        var duplicated = await verify
            .PurchaseReturns.Where(r => r.TenantId == _tenantId && r.ReturnNumber != null)
            .GroupBy(r => r.ReturnNumber)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync();

        duplicated.Should().BeEmpty("ningún ReturnNumber debe repetirse tras un lote concurrente");
    }

    // ── Test doubles mínimos ─────────────────────────────────────────────

    private sealed class FixedCurrentTenant(Func<Guid> tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId();
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Func<Guid> companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId();
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class FixedCurrentBranch(Func<Guid> branchId) : ICurrentBranch
    {
        public Guid BranchId => branchId();
        public bool IsAuthenticated => true;
        public bool HasBranchContext => true;
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => "tester";
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    private sealed class NoOpPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : MediatR.INotification => Task.CompletedTask;
    }

    /// <summary>Traductor real (no mock) contra el SqlState 23505 real de PostgreSQL.</summary>
    private sealed class RealDatabaseExceptionTranslator : IDatabaseExceptionTranslator
    {
        public bool TryGetUniqueViolation(Exception exception, out DatabaseUniqueViolationInfo info)
        {
            for (var ex = exception; ex is not null; ex = ex.InnerException)
            {
                if (ex is Npgsql.PostgresException pg && pg.SqlState == "23505")
                {
                    info = new DatabaseUniqueViolationInfo(
                        pg.SqlState,
                        pg.ConstraintName,
                        pg.TableName,
                        pg.MessageText
                    );
                    return true;
                }
            }
            info = null!;
            return false;
        }
    }
}
