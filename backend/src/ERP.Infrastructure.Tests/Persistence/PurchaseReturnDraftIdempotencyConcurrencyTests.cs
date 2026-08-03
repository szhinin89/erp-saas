using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// P0-02 Fase 5 — prueba de concurrencia de idempotencia obligatoria (diseño §16.2ter),
/// prerrequisito de implementación (no backlog) para <c>CreateDraft</c>, contra PostgreSQL real
/// (Testcontainers, sin mocks/in-memory). Cada escenario usa un <see cref="ErpDbContext"/> propio
/// por "request" — mismo criterio que un handler real bajo un scope HTTP independiente — para
/// reproducir la ventana de carrera real de <c>SaveChangesAsync</c> descrita en §16.2bis.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseReturnDraftIdempotencyConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_return_draft_idempotency_test")
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
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _userId);
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
        var paymentTerm = ERP.Domain.MasterData.Entities.PaymentTerm.Create(
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

    private async Task<Guid> CreateConfirmedInvoiceAsync(decimal lineQuantity = 10)
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
        inv.ReplaceLines(new[] { line }, _userId);
        inv.Confirm(_userId);
        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();
        return inv.Id;
    }

    private async Task<(bool Success, PurchaseReturn? Value, bool WasCreated)> ExecuteCreateAsync(
        Guid clientRequestId,
        Guid invoiceId,
        string reason,
        decimal quantity
    )
    {
        await using var db = CreateContext();
        var returnRepo = new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId));
        var invoiceRepo = new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId));
        var handler = new CreatePurchaseReturnDraftHandler(
            returnRepo,
            invoiceRepo,
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentBranch(() => _branchId),
            new FixedCurrentUser(_userId)
        );

        var invoice = await invoiceRepo.GetByIdAsync(_tenantId, invoiceId, CancellationToken.None);
        var lineId = invoice!.Lines.Single().Id;

        var cmd = new CreatePurchaseReturnDraftCommand(
            clientRequestId,
            invoiceId,
            reason,
            new[] { new PurchaseReturnDraftLineInput(lineId, quantity) }
        );

        var result = await handler.Handle(cmd, CancellationToken.None);
        return (result.IsSuccess, result.IsSuccess ? await returnRepo.GetByIdAsync(_tenantId, result.Value!.Id, CancellationToken.None) : null, result.IsSuccess);
    }

    [Fact]
    public async Task Dos_solicitudes_concurrentes_mismo_ClientRequestId_y_mismo_payload_producen_exactamente_un_efecto()
    {
        var invoiceId = await CreateConfirmedInvoiceAsync();
        var clientRequestId = Guid.NewGuid();

        var t1 = ExecuteCreateAsync(clientRequestId, invoiceId, "Producto en mal estado", 2);
        var t2 = ExecuteCreateAsync(clientRequestId, invoiceId, "Producto en mal estado", 2);
        var results = await Task.WhenAll(t1, t2);

        results.Should().OnlyContain(r => r.Success);
        results[0].Value!.Id.Should().Be(results[1].Value!.Id);

        await using var verify = CreateContext();
        var count = await verify.PurchaseReturns.CountAsync(r =>
            r.TenantId == _tenantId && r.CreateClientRequestId == clientRequestId
        );
        count.Should().Be(1);
    }

    [Fact]
    public async Task Dos_solicitudes_concurrentes_mismo_ClientRequestId_y_payload_distinto_una_tiene_exito_la_otra_PR_012()
    {
        var invoiceId = await CreateConfirmedInvoiceAsync();
        var clientRequestId = Guid.NewGuid();

        var t1 = ExecuteCreateAsync(clientRequestId, invoiceId, "Producto en mal estado", 2);
        var t2 = ExecuteCreateAsync(clientRequestId, invoiceId, "Motivo completamente distinto", 3);
        var results = await Task.WhenAll(t1, t2);

        // Ambas transacciones concurrentes completan sin excepción no controlada: una gana la
        // carrera y crea el draft (IsSuccess=true), la otra encuentra el ClientRequestId ya
        // ocupado con un payload distinto y recibe PR-012 (IsSuccess=false) — nunca una excepción
        // sin manejar ni un segundo efecto persistido.
        results.Count(r => r.Success).Should().Be(1);
        results.Count(r => !r.Success).Should().Be(1);
        await using var verify = CreateContext();
        var count = await verify.PurchaseReturns.CountAsync(r =>
            r.TenantId == _tenantId && r.CreateClientRequestId == clientRequestId
        );
        count.Should().Be(1);
    }

    [Fact]
    public async Task Claves_distintas_producen_dos_efectos_independientes()
    {
        var invoiceId = await CreateConfirmedInvoiceAsync(lineQuantity: 10);
        var cri1 = Guid.NewGuid();
        var cri2 = Guid.NewGuid();

        var t1 = ExecuteCreateAsync(cri1, invoiceId, "Motivo A", 2);
        var t2 = ExecuteCreateAsync(cri2, invoiceId, "Motivo B", 3);
        var results = await Task.WhenAll(t1, t2);

        results.Should().OnlyContain(r => r.Success);
        results[0].Value!.Id.Should().NotBe(results[1].Value!.Id);

        await using var verify = CreateContext();
        var count = await verify.PurchaseReturns.CountAsync(r => r.TenantId == _tenantId);
        count.Should().Be(2);
    }

    [Fact]
    public async Task Reintento_tras_commit_exitoso_con_mismo_ClientRequestId_y_payload_retorna_el_resultado_ya_confirmado_sin_duplicar()
    {
        var invoiceId = await CreateConfirmedInvoiceAsync();
        var clientRequestId = Guid.NewGuid();

        var first = await ExecuteCreateAsync(clientRequestId, invoiceId, "Producto en mal estado", 2);
        first.Success.Should().BeTrue();

        // Simula que el cliente nunca recibió la respuesta de la primera llamada y reintenta con
        // exactamente el mismo ClientRequestId/payload.
        var retry = await ExecuteCreateAsync(clientRequestId, invoiceId, "Producto en mal estado", 2);

        retry.Success.Should().BeTrue();
        retry.Value!.Id.Should().Be(first.Value!.Id);

        await using var verify = CreateContext();
        var count = await verify.PurchaseReturns.CountAsync(r =>
            r.TenantId == _tenantId && r.CreateClientRequestId == clientRequestId
        );
        count.Should().Be(1);
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

    /// <summary>Traductor real (no mock) contra el SqlState 23505 real de PostgreSQL — necesario para que la prueba ejerza el algoritmo de recuperación de §16.2bis de verdad.</summary>
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
