using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Sales;

/// <summary>
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para
/// <c>SalesReturnRepository</c> — Fase 3 de P0-01 (persistencia del agregado SalesReturn).
/// Cubre el mapeo EF del agregado completo (líneas + asignaciones de reembolso), el cómputo de
/// remanente devolvible y el advisory lock de concurrencia por factura.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SalesReturnRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_sales_return_repo_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _customerId;
    private Guid _cashSessionId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _createdBy
        );
        var branch = Branch.Create(
            tenant.Id,
            "Matriz",
            "Av. Principal 123",
            "001",
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
        var customer = BusinessPartner.Create(
            tenant.Id,
            "05",
            "1710034065",
            1,
            "Cliente Test",
            _createdBy
        );
        var establishment = Establishment.Create(
            tenant.Id,
            branchId: branch.Id,
            company.Id,
            code: "001",
            name: "Matriz Test",
            address: "Av. Principal 123",
            phone: null,
            isMain: true,
            createdBy: _createdBy
        );
        var cashRegister = CashRegister.Create(
            tenant.Id,
            company.Id,
            branch.Id,
            "CAJA-01",
            "Caja Principal",
            _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(customer);
        db.Establishments.Add(establishment);
        db.CashRegisters.Add(cashRegister);
        await db.SaveChangesAsync();

        var emissionPoint = EmissionPoint.Create(
            tenant.Id,
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
            tenant.Id,
            company.Id,
            branch.Id,
            _createdBy,
            cashRegister.Id,
            "CAJA-01",
            "Caja Principal",
            emissionPoint.Id,
            "001",
            0m,
            _createdBy
        );
        db.CashSessions.Add(cashSession);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _customerId = customer.Id;
        _cashSessionId = cashSession.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    // ── Helpers de siembra ────────────────────────────────────────────

    /// <summary>Crea y persiste una factura autorizada con una única línea, lista para servir de
    /// factura origen de una devolución (satisface los FKs sales_invoice_id/original_invoice_detail_id).</summary>
    private async Task<(Guid InvoiceId, Guid LineId)> SeedAuthorizedInvoiceAsync(
        string invoiceNumber,
        decimal quantity = 10m,
        decimal unitPrice = 10m
    )
    {
        await using var db = CreateContext();

        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(
            Guid.NewGuid(),
            "Contado",
            installments: 1,
            daysBetween: 0
        );

        var inv = SalesInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _customerId,
            customer,
            invoiceNumber: invoiceNumber,
            issueDate: new DateOnly(2026, 7, 25),
            createdBy: _createdBy,
            paymentTerm: paymentTerm,
            cashSessionId: _cashSessionId
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto Test",
            quantity: quantity,
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
            quantity * unitPrice
        );
        inv.ReplacePayments(new[] { payment }, _createdBy);

        inv.Authorize(_createdBy);

        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();

        return (inv.Id, line.Id);
    }

    private SalesReturn BuildDraftReturn(
        Guid salesInvoiceId,
        string returnNumber,
        string reason = "Producto en mal estado"
    ) =>
        SalesReturn.CreateDraft(
            _tenantId,
            _companyId,
            salesInvoiceId,
            _customerId,
            returnNumber,
            reason,
            _createdBy
        );

    private static SalesReturnDetail BuildLine(
        Guid returnId,
        Guid tenantId,
        Guid originalInvoiceDetailId,
        decimal quantity,
        decimal unitPrice = 10m
    ) =>
        SalesReturnDetail.Create(
            returnId,
            tenantId,
            originalInvoiceDetailId,
            "Producto Test",
            quantity,
            unitPrice,
            discountPct: 0m,
            vatCode: "0",
            vatRate: 0m,
            uomCode: "UNIT"
        );

    // ── CRUD / persistencia del agregado completo ────────────────────

    [Fact]
    public async Task AddAsync_persiste_el_agregado_completo_con_lineas_y_asignaciones()
    {
        var (invoiceId, lineId) = await SeedAuthorizedInvoiceAsync("RET-CRUD-001");

        await using var db = CreateContext();
        var repo = new SalesReturnRepository(db, new FixedCurrentCompany(_companyId));

        var salesReturn = BuildDraftReturn(invoiceId, "DEV-000001");
        var line = BuildLine(salesReturn.Id, _tenantId, lineId, quantity: 3m, unitPrice: 10m);
        salesReturn.AddLine(line, _createdBy);
        salesReturn.AddRefundAllocation(
            SalesReturnRefundAllocation.Create(
                salesReturn.Id,
                _tenantId,
                SalesReturnRefundMethod.Cash,
                30m
            ),
            _createdBy
        );
        salesReturn.Authorize(_createdBy);

        await repo.AddAsync(salesReturn);
        await repo.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var verifyRepo = new SalesReturnRepository(verifyDb, new FixedCurrentCompany(_companyId));
        var persisted = await verifyRepo.GetByIdAsync(_tenantId, salesReturn.Id);

        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(SalesReturnStatus.Authorized);
        persisted.SalesInvoiceId.Should().Be(invoiceId);
        persisted.CustomerId.Should().Be(_customerId);
        persisted.ReturnNumber.Should().Be("DEV-000001");
        persisted.AuthorizedGrandTotal.Should().Be(30m);

        persisted.Lines.Should().ContainSingle();
        persisted.Lines[0].OriginalInvoiceDetailId.Should().Be(lineId);
        persisted.Lines[0].Quantity.Should().Be(3m);
        persisted.Lines[0].IsFrozen.Should().BeTrue();

        persisted.RefundAllocations.Should().ContainSingle();
        persisted.RefundAllocations[0].Method.Should().Be(SalesReturnRefundMethod.Cash);
        persisted.RefundAllocations[0].Amount.Should().Be(30m);
    }

    [Fact]
    public async Task GetByIdAsync_con_id_inexistente_retorna_null()
    {
        await using var db = CreateContext();
        var repo = new SalesReturnRepository(db, new FixedCurrentCompany(_companyId));

        var result = await repo.GetByIdAsync(_tenantId, Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPagedAsync_filtra_por_estado()
    {
        var (invoiceId, lineId) = await SeedAuthorizedInvoiceAsync("RET-PAGED-001");

        await using var db = CreateContext();
        var repo = new SalesReturnRepository(db, new FixedCurrentCompany(_companyId));

        var authorized = BuildDraftReturn(invoiceId, "DEV-PAGED-001");
        authorized.AddLine(
            BuildLine(authorized.Id, _tenantId, lineId, quantity: 1m, unitPrice: 10m),
            _createdBy
        );
        authorized.AddRefundAllocation(
            SalesReturnRefundAllocation.Create(
                authorized.Id,
                _tenantId,
                SalesReturnRefundMethod.Cash,
                10m
            ),
            _createdBy
        );
        authorized.Authorize(_createdBy);

        var draft = BuildDraftReturn(invoiceId, "DEV-PAGED-002");

        await repo.AddAsync(authorized);
        await repo.AddAsync(draft);
        await repo.SaveChangesAsync();

        var (items, total) = await repo.GetPagedAsync(_tenantId, null, "Authorized", 1, 10);

        total.Should().Be(1);
        items.Should().ContainSingle().Which.Id.Should().Be(authorized.Id);
    }

    // ── GetReturnedQuantityByInvoiceDetailAsync ───────────────────────

    [Fact]
    public async Task GetReturnedQuantityByInvoiceDetailAsync_sin_devoluciones_retorna_cero()
    {
        var (_, lineId) = await SeedAuthorizedInvoiceAsync("RET-QTY-001");

        await using var db = CreateContext();
        var repo = new SalesReturnRepository(db, new FixedCurrentCompany(_companyId));

        var qty = await repo.GetReturnedQuantityByInvoiceDetailAsync(_tenantId, lineId);

        qty.Should().Be(0m);
    }

    [Fact]
    public async Task GetReturnedQuantityByInvoiceDetailAsync_suma_solo_devoluciones_autorizadas()
    {
        var (invoiceId, lineId) = await SeedAuthorizedInvoiceAsync(
            "RET-QTY-002",
            quantity: 10m,
            unitPrice: 10m
        );

        await using var db = CreateContext();
        var repo = new SalesReturnRepository(db, new FixedCurrentCompany(_companyId));

        // Devolución autorizada por 2 unidades — sí debe contar.
        var authorized = BuildDraftReturn(invoiceId, "DEV-QTY-001");
        authorized.AddLine(
            BuildLine(authorized.Id, _tenantId, lineId, quantity: 2m, unitPrice: 10m),
            _createdBy
        );
        authorized.AddRefundAllocation(
            SalesReturnRefundAllocation.Create(
                authorized.Id,
                _tenantId,
                SalesReturnRefundMethod.Cash,
                20m
            ),
            _createdBy
        );
        authorized.Authorize(_createdBy);

        // Devolución en Draft por 5 unidades — no debe contar.
        var draft = BuildDraftReturn(invoiceId, "DEV-QTY-002");
        draft.AddLine(
            BuildLine(draft.Id, _tenantId, lineId, quantity: 5m, unitPrice: 10m),
            _createdBy
        );

        await repo.AddAsync(authorized);
        await repo.AddAsync(draft);
        await repo.SaveChangesAsync();

        var qty = await repo.GetReturnedQuantityByInvoiceDetailAsync(_tenantId, lineId);

        qty.Should().Be(2m);
    }

    [Fact]
    public async Task GetReturnedQuantityByInvoiceDetailAsync_ignora_devoluciones_canceladas()
    {
        var (invoiceId, lineId) = await SeedAuthorizedInvoiceAsync(
            "RET-QTY-003",
            quantity: 10m,
            unitPrice: 10m
        );

        await using var db = CreateContext();
        var repo = new SalesReturnRepository(db, new FixedCurrentCompany(_companyId));

        var cancelled = BuildDraftReturn(invoiceId, "DEV-QTY-003");
        cancelled.AddLine(
            BuildLine(cancelled.Id, _tenantId, lineId, quantity: 4m, unitPrice: 10m),
            _createdBy
        );
        cancelled.Cancel(_createdBy);

        await repo.AddAsync(cancelled);
        await repo.SaveChangesAsync();

        var qty = await repo.GetReturnedQuantityByInvoiceDetailAsync(_tenantId, lineId);

        qty.Should().Be(0m);
    }

    // ── AcquireReturnLockAsync — advisory lock por factura ────────────

    [Fact]
    public async Task AcquireReturnLockAsync_serializa_transacciones_para_la_misma_factura()
    {
        var (invoiceId, _) = await SeedAuthorizedInvoiceAsync("RET-LOCK-001");

        await using var dbA = CreateContext();
        await using var txA = await dbA.Database.BeginTransactionAsync();
        var repoA = new SalesReturnRepository(dbA, new FixedCurrentCompany(_companyId));
        await repoA.AcquireReturnLockAsync(_tenantId, invoiceId);

        var taskB = Task.Run(async () =>
        {
            await using var dbB = CreateContext();
            await using var txB = await dbB.Database.BeginTransactionAsync();
            var repoB = new SalesReturnRepository(dbB, new FixedCurrentCompany(_companyId));
            await repoB.AcquireReturnLockAsync(_tenantId, invoiceId);
            await txB.CommitAsync();
        });

        // Mientras A mantiene la transacción abierta (sin comitear), B debe quedar bloqueada.
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        taskB
            .IsCompleted.Should()
            .BeFalse(
                because: "el advisory lock debe bloquear a B mientras A mantiene la transacción abierta para la misma factura"
            );

        await txA.CommitAsync(); // libera el advisory lock

        var completedInTime = await Task.WhenAny(taskB, Task.Delay(TimeSpan.FromSeconds(5)));
        completedInTime
            .Should()
            .Be(taskB, because: "B debe desbloquearse y completar apenas A libera el lock");
        await taskB;
    }

    [Fact]
    public async Task AcquireReturnLockAsync_no_bloquea_facturas_distintas()
    {
        var (invoiceIdA, _) = await SeedAuthorizedInvoiceAsync("RET-LOCK-002");
        var (invoiceIdB, _) = await SeedAuthorizedInvoiceAsync("RET-LOCK-003");

        await using var dbA = CreateContext();
        await using var txA = await dbA.Database.BeginTransactionAsync();
        var repoA = new SalesReturnRepository(dbA, new FixedCurrentCompany(_companyId));
        await repoA.AcquireReturnLockAsync(_tenantId, invoiceIdA); // sin comitear todavía

        await using var dbB = CreateContext();
        await using var txB = await dbB.Database.BeginTransactionAsync();
        var repoB = new SalesReturnRepository(dbB, new FixedCurrentCompany(_companyId));
        var lockTaskB = repoB.AcquireReturnLockAsync(_tenantId, invoiceIdB);

        var winner = await Task.WhenAny(lockTaskB, Task.Delay(TimeSpan.FromSeconds(3)));
        winner
            .Should()
            .Be(
                lockTaskB,
                because: "el advisory lock de una factura distinta no debe bloquearse por el lock de otra factura"
            );
        await lockTaskB;

        await txB.CommitAsync();
        await txA.CommitAsync();
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
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
