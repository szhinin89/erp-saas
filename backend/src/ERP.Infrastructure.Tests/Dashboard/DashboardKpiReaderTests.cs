using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Dashboard;

/// <summary>
/// DASHBOARD-KPI-REAL-DATA-01 — cubre <see cref="DashboardKpiReader"/> contra Postgres real
/// (Testcontainers): cada escenario de Ventas/CxC/CxP crea su propia Company aislada (con su
/// propia cadena Branch/Establishment/CashRegister/EmissionPoint/CashSession) para que los
/// montos esperados sean exactos sin depender del orden de ejecución de los demás [Fact] — el
/// Tenant/Cliente/Proveedor/ItemType son tenant-scoped y se comparten sin riesgo de colisión.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class DashboardKpiReaderTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_dashboard_kpi_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _customerId;
    private Guid _supplierId;
    private Guid _itemTypeId;
    private readonly Guid _createdBy = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.Empty);
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var customer = BusinessPartner.Create(
            tenant.Id,
            "05",
            "1710034065",
            1,
            "Cliente Test",
            _createdBy
        );
        // Tipo "06" (Pasaporte) — sin dígito verificador, evita colisión con la cédula del
        // cliente y no depende del algoritmo del Registro Civil (irrelevante para este test).
        var supplier = BusinessPartner.Create(
            tenant.Id,
            "06",
            "SUPPLIER0001",
            2,
            "Proveedor Test",
            _createdBy
        );
        var itemType = ItemTypeDefinition.Create(tenant.Id, "MERCH", "Mercadería", 1, _createdBy);

        db.Tenants.Add(tenant);
        db.BusinessPartners.AddRange(customer, supplier);
        db.Set<ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _customerId = customer.Id;
        _supplierId = supplier.Id;
        _itemTypeId = itemType.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid companyId) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                // Requerido por SalesInvoice.Authorize() → SalesInvoiceDetail.Freeze(), que agrega
                // una entidad hija nueva (tax) a la colección de un agregado ya trackeado entre dos
                // SaveChanges — sin este interceptor (que sí registra DependencyInjection.cs en
                // producción) EF la clasifica Modified en vez de Added y lanza
                // DbUpdateConcurrencyException. Ver NewChildEntityTrackingInterceptor.cs.
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );

    /// <summary>Crea una Company nueva con toda la infraestructura de Ventas (Branch,
    /// Establishment, CashRegister, EmissionPoint, CashSession) y una Warehouse — todo lo
    /// necesario para autorizar facturas y registrar stock en esa Company aislada.</summary>
    private async Task<SalesInfra> CreateSalesInfraAsync(ErpDbContext db, string suffix)
    {
        var company = Company.CreateManaged(
            _tenantId,
            $"179001234{suffix}",
            $"Empresa {suffix}",
            createdBy: _createdBy
        );
        var branch = Branch.Create(
            _tenantId,
            $"Matriz {suffix}",
            "Av. Principal 123",
            $"B{suffix}",
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
            true,
            _createdBy,
            companyId: company.Id
        );
        var establishment = Establishment.Create(
            _tenantId,
            branchId: branch.Id,
            company.Id,
            code: "001",
            name: $"Matriz {suffix}",
            address: "Av. Principal 123",
            phone: null,
            isMain: true,
            createdBy: _createdBy
        );
        var cashRegister = CashRegister.Create(
            _tenantId,
            company.Id,
            branch.Id,
            $"CAJA-{suffix}",
            "Caja Principal",
            _createdBy
        );
        var warehouse = Warehouse.Create(
            _tenantId,
            branch.Id,
            $"Bodega {suffix}",
            $"BOD-{suffix}",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _createdBy,
            company.Id,
            isMain: true
        );

        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(cashRegister);
        db.Set<Warehouse>().Add(warehouse);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            _tenantId,
            company.Id,
            establishment.Id,
            code: "001",
            name: "PE-001",
            emissionType: EmissionType.Electronic,
            isDefault: true,
            createdBy: _createdBy
        );
        db.EmissionPoints.Add(emissionPoint);
        await db.SaveChangesAsync();

        var cashSession = CashSession.Open(
            _tenantId,
            company.Id,
            branch.Id,
            _createdBy,
            cashRegister.Id,
            $"CAJA-{suffix}",
            "Caja Principal",
            emissionPoint.Id,
            "001",
            0m,
            _createdBy
        );
        db.CashSessions.Add(cashSession);
        await db.SaveChangesAsync();

        return new SalesInfra(company.Id, branch.Id, cashSession.Id, warehouse.Id);
    }

    private async Task<SalesInvoice> AuthorizeInvoiceAsync(
        ErpDbContext db,
        SalesInfra infra,
        DateOnly issueDate,
        string invoiceNumber,
        decimal unitPrice
    )
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(
            Guid.NewGuid(),
            "Contado",
            installments: 1,
            daysBetween: 0
        );

        var inv = SalesInvoice.CreateDraft(
            _tenantId,
            infra.CompanyId,
            infra.BranchId,
            _customerId,
            customer,
            invoiceNumber: invoiceNumber,
            issueDate: issueDate,
            createdBy: _createdBy,
            paymentTerm: paymentTerm,
            cashSessionId: infra.CashSessionId,
            emissionPointId: null
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto Test",
            quantity: 1,
            unitPrice: unitPrice,
            vatCode: "0",
            uomCode: "UNIT"
        );
        inv.ReplaceLines(new[] { line }, _createdBy);

        var payment = SalesInvoicePayment.Create(
            inv.Id,
            _tenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            unitPrice
        );
        inv.ReplacePayments(new[] { payment }, _createdBy);

        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        inv.Authorize(_createdBy);
        await db.SaveChangesAsync();

        return inv;
    }

    // ── Sin datos ────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_sin_datos_devuelve_todos_los_kpis_en_cero()
    {
        await using var seed = CreateContext(Guid.Empty);
        var infra = await CreateSalesInfraAsync(seed, "0001");

        await using var read = CreateContext(infra.CompanyId);
        var reader = new DashboardKpiReader(read, new FixedCurrentCompany(infra.CompanyId));

        var result = await reader.ReadAsync(_tenantId, infra.CompanyId, new DateTime(2026, 8, 15));

        result.SalesMtd.Should().Be(0m);
        result.InvoicesMtd.Should().Be(0);
        result.SalesYtd.Should().Be(0m);
        result.PendingArTotal.Should().Be(0m);
        result.PendingArCount.Should().Be(0);
        result.OverdueArTotal.Should().Be(0m);
        result.OverdueArCount.Should().Be(0);
        result.PendingApTotal.Should().Be(0m);
        result.PendingApCount.Should().Be(0);
        result.OverdueApTotal.Should().Be(0m);
        result.OverdueApCount.Should().Be(0);
        result.LowStockSkuCount.Should().Be(0);
        result.OutOfStockSkuCount.Should().Be(0);
    }

    // ── Ventas ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_ventas_del_mes_excluye_facturas_de_otro_mes()
    {
        await using var seed = CreateContext(Guid.Empty);
        var infra = await CreateSalesInfraAsync(seed, "0002");

        await AuthorizeInvoiceAsync(seed, infra, new DateOnly(2026, 8, 10), "001-001-000000001", 100m);
        await AuthorizeInvoiceAsync(seed, infra, new DateOnly(2026, 7, 10), "001-001-000000002", 250m);

        await using var read = CreateContext(infra.CompanyId);
        var reader = new DashboardKpiReader(read, new FixedCurrentCompany(infra.CompanyId));
        var result = await reader.ReadAsync(_tenantId, infra.CompanyId, new DateTime(2026, 8, 20));

        result.SalesMtd.Should().Be(100m);
        result.InvoicesMtd.Should().Be(1);
    }

    [Fact]
    public async Task ReadAsync_ventas_del_anio_incluye_el_anio_y_excluye_otros_anios()
    {
        await using var seed = CreateContext(Guid.Empty);
        var infra = await CreateSalesInfraAsync(seed, "0003");

        await AuthorizeInvoiceAsync(seed, infra, new DateOnly(2026, 2, 5), "001-001-000000001", 100m);
        await AuthorizeInvoiceAsync(seed, infra, new DateOnly(2026, 8, 10), "001-001-000000002", 50m);
        await AuthorizeInvoiceAsync(seed, infra, new DateOnly(2025, 12, 31), "001-001-000000003", 999m);

        await using var read = CreateContext(infra.CompanyId);
        var reader = new DashboardKpiReader(read, new FixedCurrentCompany(infra.CompanyId));
        var result = await reader.ReadAsync(_tenantId, infra.CompanyId, new DateTime(2026, 8, 20));

        result.SalesYtd.Should().Be(150m);
    }

    [Fact]
    public async Task ReadAsync_facturas_anuladas_no_suman_a_ventas()
    {
        await using var seed = CreateContext(Guid.Empty);
        var infra = await CreateSalesInfraAsync(seed, "0004");

        var inv = await AuthorizeInvoiceAsync(
            seed,
            infra,
            new DateOnly(2026, 8, 10),
            "001-001-000000001",
            100m
        );

        // Se recarga en un contexto nuevo antes de anular: el mismo contexto que autoriza
        // ya viene de encadenar dos SaveChangesAsync (Draft→insert, Authorize→update con evento
        // de dominio) y arrastraba un xmin desactualizado para una tercera escritura.
        await using (var cancelDb = CreateContext(infra.CompanyId))
        {
            var toCancel = await cancelDb.SalesInvoices.SingleAsync(x => x.Id == inv.Id);
            toCancel.Cancel("Prueba de anulación", _createdBy);
            await cancelDb.SaveChangesAsync();
        }

        await using var read = CreateContext(infra.CompanyId);
        var reader = new DashboardKpiReader(read, new FixedCurrentCompany(infra.CompanyId));
        var result = await reader.ReadAsync(_tenantId, infra.CompanyId, new DateTime(2026, 8, 20));

        result.SalesMtd.Should().Be(0m);
        result.InvoicesMtd.Should().Be(0);
        result.SalesYtd.Should().Be(0m);
    }

    // ── Cuentas por cobrar ───────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_cxc_pendiente_suma_solo_saldos_abiertos()
    {
        await using var seed = CreateContext(Guid.Empty);
        var infra = await CreateSalesInfraAsync(seed, "0005");
        var invoice1 = await AuthorizeInvoiceAsync(
            seed,
            infra,
            new DateOnly(2026, 8, 1),
            "001-001-000000001",
            100m
        );
        var invoice2 = await AuthorizeInvoiceAsync(
            seed,
            infra,
            new DateOnly(2026, 8, 1),
            "001-001-000000002",
            200m
        );

        var openReceivable = SalesReceivable.Create(
            _tenantId,
            infra.CompanyId,
            invoice1.Id,
            _customerId,
            100m,
            _createdBy
        );
        openReceivable.GenerateInstallments(new DateOnly(2026, 9, 1), creditTermDays: 30, installmentCount: 1);

        var settledReceivable = SalesReceivable.Create(
            _tenantId,
            infra.CompanyId,
            invoice2.Id,
            _customerId,
            200m,
            _createdBy
        );
        settledReceivable.GenerateInstallments(
            new DateOnly(2026, 9, 1),
            creditTermDays: 30,
            installmentCount: 1
        );
        settledReceivable.RegisterCollection(200m, _createdBy);

        seed.SalesReceivables.AddRange(openReceivable, settledReceivable);
        await seed.SaveChangesAsync();

        await using var read = CreateContext(infra.CompanyId);
        var reader = new DashboardKpiReader(read, new FixedCurrentCompany(infra.CompanyId));
        var result = await reader.ReadAsync(_tenantId, infra.CompanyId, new DateTime(2026, 8, 20));

        result.PendingArTotal.Should().Be(100m);
        result.PendingArCount.Should().Be(1);
    }

    [Fact]
    public async Task ReadAsync_cxc_vencida_solo_suma_las_que_pasaron_su_vencimiento()
    {
        await using var seed = CreateContext(Guid.Empty);
        var infra = await CreateSalesInfraAsync(seed, "0006");
        var overdueInvoice = await AuthorizeInvoiceAsync(
            seed,
            infra,
            new DateOnly(2026, 6, 1),
            "001-001-000000001",
            100m
        );
        var currentInvoice = await AuthorizeInvoiceAsync(
            seed,
            infra,
            new DateOnly(2026, 8, 1),
            "001-001-000000002",
            300m
        );

        var overdue = SalesReceivable.Create(
            _tenantId,
            infra.CompanyId,
            overdueInvoice.Id,
            _customerId,
            100m,
            _createdBy
        );
        overdue.GenerateInstallments(new DateOnly(2026, 6, 1), creditTermDays: 30, installmentCount: 1);

        var current = SalesReceivable.Create(
            _tenantId,
            infra.CompanyId,
            currentInvoice.Id,
            _customerId,
            300m,
            _createdBy
        );
        current.GenerateInstallments(new DateOnly(2026, 8, 1), creditTermDays: 60, installmentCount: 1);

        seed.SalesReceivables.AddRange(overdue, current);
        await seed.SaveChangesAsync();

        await using var read = CreateContext(infra.CompanyId);
        var reader = new DashboardKpiReader(read, new FixedCurrentCompany(infra.CompanyId));
        // asOf = 2026-08-20: la cuota de "overdue" vence 2026-07-01 (pasada); la de "current"
        // vence 2026-10-01 (futura) — solo "overdue" debe contar como vencida.
        var result = await reader.ReadAsync(_tenantId, infra.CompanyId, new DateTime(2026, 8, 20));

        result.PendingArTotal.Should().Be(400m);
        result.OverdueArTotal.Should().Be(100m);
        result.OverdueArCount.Should().Be(1);
    }

    // ── Cuentas por pagar ────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_cxp_pendiente_suma_solo_saldos_abiertos()
    {
        await using var seed = CreateContext(Guid.Empty);
        var company = Company.CreateManaged(
            _tenantId,
            "1790012340007",
            "Empresa 0007",
            createdBy: _createdBy
        );
        var branch = MinimalBranch(company.Id, "0007");
        seed.Companies.Add(company);
        seed.Branches.Add(branch);
        await seed.SaveChangesAsync();

        var openPayable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            company.Id,
            branch.Id,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            Guid.NewGuid(),
            "01",
            "001-001-000000001",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 1),
            _createdBy
        );
        openPayable.AddInstallment(1, new DateOnly(2026, 9, 1), 150m);

        var paidPayable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            company.Id,
            branch.Id,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            Guid.NewGuid(),
            "01",
            "001-001-000000002",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 1),
            _createdBy
        );
        paidPayable.AddInstallment(1, new DateOnly(2026, 9, 1), 300m);
        paidPayable.RegisterPayment(300m, _createdBy);

        seed.AccountsPayables.AddRange(openPayable, paidPayable);
        await seed.SaveChangesAsync();

        await using var read = CreateContext(company.Id);
        var reader = new DashboardKpiReader(read, new FixedCurrentCompany(company.Id));
        var result = await reader.ReadAsync(_tenantId, company.Id, new DateTime(2026, 8, 20));

        result.PendingApTotal.Should().Be(150m);
        result.PendingApCount.Should().Be(1);
    }

    [Fact]
    public async Task ReadAsync_cxp_vencida_solo_suma_las_que_pasaron_su_vencimiento()
    {
        await using var seed = CreateContext(Guid.Empty);
        var company = Company.CreateManaged(
            _tenantId,
            "1790012340008",
            "Empresa 0008",
            createdBy: _createdBy
        );
        var branch = MinimalBranch(company.Id, "0008");
        seed.Companies.Add(company);
        seed.Branches.Add(branch);
        await seed.SaveChangesAsync();

        var overduePayable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            company.Id,
            branch.Id,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            Guid.NewGuid(),
            "01",
            "001-001-000000001",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 1),
            _createdBy
        );
        overduePayable.AddInstallment(1, new DateOnly(2026, 7, 1), 150m);

        var currentPayable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            company.Id,
            branch.Id,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            Guid.NewGuid(),
            "01",
            "001-001-000000002",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 1),
            _createdBy
        );
        currentPayable.AddInstallment(1, new DateOnly(2026, 10, 1), 400m);

        seed.AccountsPayables.AddRange(overduePayable, currentPayable);
        await seed.SaveChangesAsync();

        await using var read = CreateContext(company.Id);
        var reader = new DashboardKpiReader(read, new FixedCurrentCompany(company.Id));
        var result = await reader.ReadAsync(_tenantId, company.Id, new DateTime(2026, 8, 20));

        result.PendingApTotal.Should().Be(550m);
        result.OverdueApTotal.Should().Be(150m);
        result.OverdueApCount.Should().Be(1);
    }

    // ── Inventario ───────────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_calcula_stock_bajo_y_sin_stock_por_sku_sin_duplicar_por_bodega()
    {
        await using var seed = CreateContext(Guid.Empty);
        var infra = await CreateSalesInfraAsync(seed, "0009");
        var warehouse2 = Warehouse.Create(
            _tenantId,
            infra.BranchId,
            "Bodega 2",
            "BOD-0009-2",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _createdBy,
            infra.CompanyId
        );
        seed.Set<Warehouse>().Add(warehouse2);
        await seed.SaveChangesAsync();

        var outOfStockItem = await CreateItemAsync(seed, minStockQty: 5m);
        var lowStockItem = await CreateItemAsync(seed, minStockQty: 10m);
        var normalItem = await CreateItemAsync(seed, minStockQty: 10m);
        await CreateItemAsync(seed, minStockQty: 5m); // sin CurrentStock — cuenta como sin stock

        // outOfStockItem: 0 en total (nunca se le aplicó movimiento).
        var outOfStockStock = CurrentStock.Create(
            _tenantId,
            outOfStockItem.Id,
            infra.WarehouseId,
            _createdBy,
            infra.CompanyId
        );

        // lowStockItem: repartido en 2 bodegas (3 + 4 = 7, bajo el mínimo de 10) — no debe
        // contarse dos veces por tener presencia en 2 bodegas.
        var lowStockA = CurrentStock.Create(
            _tenantId,
            lowStockItem.Id,
            infra.WarehouseId,
            _createdBy,
            infra.CompanyId
        );
        lowStockA.ApplyMovement(3m, _createdBy);
        var lowStockB = CurrentStock.Create(
            _tenantId,
            lowStockItem.Id,
            warehouse2.Id,
            _createdBy,
            infra.CompanyId
        );
        lowStockB.ApplyMovement(4m, _createdBy);

        // normalItem: 50 unidades, por encima del mínimo de 10.
        var normalStock = CurrentStock.Create(
            _tenantId,
            normalItem.Id,
            infra.WarehouseId,
            _createdBy,
            infra.CompanyId
        );
        normalStock.ApplyMovement(50m, _createdBy);

        seed.Set<CurrentStock>()
            .AddRange(outOfStockStock, lowStockA, lowStockB, normalStock);
        await seed.SaveChangesAsync();
        // noMovementItem nunca recibe fila de CurrentStock — debe contar como sin stock (0).

        await using var read = CreateContext(infra.CompanyId);
        var reader = new DashboardKpiReader(read, new FixedCurrentCompany(infra.CompanyId));
        var result = await reader.ReadAsync(_tenantId, infra.CompanyId, new DateTime(2026, 8, 20));

        result.OutOfStockSkuCount.Should().Be(2); // outOfStockItem + noMovementItem
        result.LowStockSkuCount.Should().Be(1); // lowStockItem (3+4=7 <= 10), una sola vez
    }

    private async Task<Item> CreateItemAsync(ErpDbContext db, decimal minStockQty)
    {
        var item = Item.Create(
            _tenantId,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto Test",
            description: "Producto Test",
            itemTypeId: _itemTypeId,
            defaultUomCode: "UNIT",
            taxConfig: ItemTaxConfig.Create(saleVatCode: "0", purchaseVatCode: "0"),
            saleConfig: ItemSaleConfig.Create(isForSale: true),
            stockConfig: ItemStockConfig.Create(tracksStock: true, minStockQty: minStockQty),
            createdBy: _createdBy
        );
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    // ── Aislamiento multi-tenant/company ────────────────────────────────

    [Fact]
    public async Task ReadAsync_no_ve_cuentas_por_pagar_de_otra_empresa()
    {
        await using var seed = CreateContext(Guid.Empty);
        var companyA = Company.CreateManaged(
            _tenantId,
            "1790012340010",
            "Empresa A-0010",
            createdBy: _createdBy
        );
        var companyB = Company.CreateManaged(
            _tenantId,
            "1790012340011",
            "Empresa B-0010",
            createdBy: _createdBy
        );
        var branchA = MinimalBranch(companyA.Id, "0010A");
        var branchB = MinimalBranch(companyB.Id, "0010B");
        seed.Companies.AddRange(companyA, companyB);
        seed.Branches.AddRange(branchA, branchB);
        await seed.SaveChangesAsync();

        var payableB = AccountsPayable.CreateFromOrigin(
            _tenantId,
            companyB.Id,
            branchB.Id,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            Guid.NewGuid(),
            "01",
            "001-001-000000001",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 1),
            _createdBy
        );
        payableB.AddInstallment(1, new DateOnly(2026, 9, 1), 500m);
        seed.AccountsPayables.Add(payableB);
        await seed.SaveChangesAsync();

        await using var read = CreateContext(companyA.Id);
        var reader = new DashboardKpiReader(read, new FixedCurrentCompany(companyA.Id));
        var result = await reader.ReadAsync(_tenantId, companyA.Id, new DateTime(2026, 8, 20));

        result.PendingApTotal.Should().Be(0m);
        result.PendingApCount.Should().Be(0);
    }

    private Branch MinimalBranch(Guid companyId, string suffix) =>
        Branch.Create(
            _tenantId,
            $"Sucursal {suffix}",
            "Av. Principal 123",
            $"S{suffix}",
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
            true,
            _createdBy,
            companyId: companyId
        );

    private sealed record SalesInfra(Guid CompanyId, Guid BranchId, Guid CashSessionId, Guid WarehouseId);

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
