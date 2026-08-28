using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Payables;

/// <summary>
/// PAYABLES-READ-API-11 — <c>AccountsPayableRepository.SearchAsync</c> combina un filtro por
/// <c>OriginType</c>/<c>Status</c>/<c>SupplierId</c>, un rango de vencimiento derivado de
/// <c>Installments.Min(DueDate)</c> y una búsqueda por documento/proveedor que hace un subquery
/// join contra <c>BusinessPartners</c> — necesita Postgres real porque la traducción de ese LINQ
/// (agregado correlacionado + <c>Contains</c> sobre subquery) no es verificable con mocks.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class AccountsPayableSearchTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_payables_search_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private readonly Guid _userId = Guid.NewGuid();

    private Guid _acmeSupplierId;
    private Guid _globexSupplierId;

    private Guid _purchasePendingId;
    private Guid _purchasePaidId;
    private Guid _expensePendingId;

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

        var acme = BusinessPartner.Create(_tenantId, "05", "1710034065", 1, "Acme Distribuidora", _userId);
        var globex = BusinessPartner.Create(_tenantId, "05", "1710034073", 1, "Globex Corp", _userId);
        db.BusinessPartners.AddRange(acme, globex);
        await db.SaveChangesAsync();
        _acmeSupplierId = acme.Id;
        _globexSupplierId = globex.Id;

        var issueDate = new DateOnly(2026, 8, 1);

        var purchasePending = AccountsPayable.CreateFromOrigin(
            _tenantId, _companyId, _branchId, _acmeSupplierId,
            AccountsPayableOriginType.PurchaseInvoice, Guid.NewGuid(),
            "01", "001-001-000000010", issueDate, issueDate, _userId
        );
        purchasePending.AddInstallment(1, new DateOnly(2026, 9, 15), 500m);
        _purchasePendingId = purchasePending.Id;

        var purchasePaid = AccountsPayable.CreateFromOrigin(
            _tenantId, _companyId, _branchId, _globexSupplierId,
            AccountsPayableOriginType.PurchaseInvoice, Guid.NewGuid(),
            "01", "001-001-000000020", issueDate, issueDate, _userId
        );
        purchasePaid.AddInstallment(1, new DateOnly(2026, 8, 20), 300m);
        purchasePaid.RegisterPayment(300m, _userId);
        _purchasePaidId = purchasePaid.Id;

        var expensePending = AccountsPayable.CreateFromOrigin(
            _tenantId, _companyId, _branchId, _acmeSupplierId,
            AccountsPayableOriginType.ExpenseDocument, Guid.NewGuid(),
            "GTO", "GTO-000001", issueDate, issueDate, _userId
        );
        expensePending.AddInstallment(1, new DateOnly(2026, 10, 1), 150m);
        _expensePendingId = expensePending.Id;

        db.Set<AccountsPayable>().AddRange(purchasePending, purchasePaid, expensePending);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );

    [Fact]
    public async Task Filtra_por_OriginType_PurchaseInvoice()
    {
        await using var db = CreateContext();
        var repo = new AccountsPayableRepository(db);

        var (items, total) = await repo.SearchAsync(
            _tenantId, _companyId, AccountsPayableOriginType.PurchaseInvoice,
            null, null, null, null, null, 1, 50
        );

        total.Should().Be(2);
        items.Select(x => x.Id).Should().BeEquivalentTo([_purchasePendingId, _purchasePaidId]);
    }

    [Fact]
    public async Task Filtra_por_OriginType_ExpenseDocument()
    {
        await using var db = CreateContext();
        var repo = new AccountsPayableRepository(db);

        var (items, total) = await repo.SearchAsync(
            _tenantId, _companyId, AccountsPayableOriginType.ExpenseDocument,
            null, null, null, null, null, 1, 50
        );

        total.Should().Be(1);
        items.Should().ContainSingle(x => x.Id == _expensePendingId);
    }

    [Fact]
    public async Task Filtra_por_Status_Paid()
    {
        await using var db = CreateContext();
        var repo = new AccountsPayableRepository(db);

        var (items, total) = await repo.SearchAsync(
            _tenantId, _companyId, null,
            AccountsPayableStatus.Paid, null, null, null, null, 1, 50
        );

        total.Should().Be(1);
        items.Should().ContainSingle(x => x.Id == _purchasePaidId);
    }

    [Fact]
    public async Task Filtra_por_Status_Pending()
    {
        await using var db = CreateContext();
        var repo = new AccountsPayableRepository(db);

        var (items, total) = await repo.SearchAsync(
            _tenantId, _companyId, null,
            AccountsPayableStatus.Pending, null, null, null, null, 1, 50
        );

        total.Should().Be(2);
        items.Select(x => x.Id).Should().BeEquivalentTo([_purchasePendingId, _expensePendingId]);
    }

    [Fact]
    public async Task Busqueda_por_numero_de_documento_encuentra_la_cuenta()
    {
        await using var db = CreateContext();
        var repo = new AccountsPayableRepository(db);

        var (items, total) = await repo.SearchAsync(
            _tenantId, _companyId, null, null, null, null, null,
            "000000020", 1, 50
        );

        total.Should().Be(1);
        items.Should().ContainSingle(x => x.Id == _purchasePaidId);
    }

    [Fact]
    public async Task Busqueda_por_nombre_de_proveedor_encuentra_todas_sus_cuentas()
    {
        await using var db = CreateContext();
        var repo = new AccountsPayableRepository(db);

        var (items, total) = await repo.SearchAsync(
            _tenantId, _companyId, null, null, null, null, null,
            "acme", 1, 50
        );

        total.Should().Be(2);
        items.Select(x => x.Id).Should().BeEquivalentTo([_purchasePendingId, _expensePendingId]);
    }

    [Fact]
    public async Task Filtra_por_rango_de_vencimiento_usando_el_minimo_entre_cuotas()
    {
        await using var db = CreateContext();
        var repo = new AccountsPayableRepository(db);

        var (items, total) = await repo.SearchAsync(
            _tenantId, _companyId, null, null, null,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30),
            null, 1, 50
        );

        total.Should().Be(1);
        items.Should().ContainSingle(x => x.Id == _purchasePendingId);
    }

    [Fact]
    public async Task Incluye_siempre_las_cuotas_para_que_los_saldos_no_sean_cero_por_defecto()
    {
        await using var db = CreateContext();
        var repo = new AccountsPayableRepository(db);

        var (items, _) = await repo.SearchAsync(
            _tenantId, _companyId, AccountsPayableOriginType.PurchaseInvoice,
            null, null, null, null, null, 1, 50
        );

        var pending = items.Single(x => x.Id == _purchasePendingId);
        pending.Installments.Should().ContainSingle();
        pending.TotalAmount.Should().Be(500m);
        pending.OutstandingAmount.Should().Be(500m);
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId { get; } = tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId { get; } = companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class NoOpPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : MediatR.INotification => Task.CompletedTask;
    }
}
