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

namespace ERP.Infrastructure.Tests.Persistence.Purchases;

/// <summary>
/// ERP-CORE-CLOSEOUT-08 / PAYABLES-PURCHASE-MIGRATION-10 — cubre el filtro por Status de
/// <c>AccountsPayableRepository.GetPagedAsync</c> contra el modelo genérico de CxP. A diferencia
/// del <c>PurchasePayable</c> original (donde "paid"/"pending" se traducían por saldo en tiempo de
/// query, con el bug histórico documentado aquí), <c>AccountsPayable.Status</c> ahora es un enum
/// mantenido por el propio dominio (<c>RecalculateStatus</c> tras cada Apply/Reverse en las
/// cuotas) — este test verifica que el filtro por enum real sigue devolviendo los resultados
/// correctos. Usa Postgres real porque valida la traducción de la query EF, no solo lógica de
/// dominio ya cubierta por pruebas unitarias.
/// </summary>
public sealed class PurchasePayableRepositoryStatusFilterTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_payable_status_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private readonly Guid _userId = Guid.NewGuid();
    private Guid _paidPayableId;
    private Guid _pendingPayableId;

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

        var supplier = BusinessPartner.Create(_tenantId, "05", "1710034065", 1, "Proveedor Test", _userId);
        db.BusinessPartners.Add(supplier);
        await db.SaveChangesAsync();
        _supplierId = supplier.Id;

        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var paid = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            Guid.NewGuid(),
            "01",
            "001-001-000000001",
            issueDate,
            issueDate,
            _userId
        );
        paid.AddInstallment(1, issueDate.AddDays(30), 100m);
        paid.RegisterPayment(100m, _userId);
        _paidPayableId = paid.Id;

        var pending = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            Guid.NewGuid(),
            "01",
            "001-001-000000002",
            issueDate,
            issueDate,
            _userId
        );
        pending.AddInstallment(1, issueDate.AddDays(30), 50m);
        _pendingPayableId = pending.Id;

        db.Set<AccountsPayable>().AddRange(paid, pending);
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
    public async Task Filtro_Paid_devuelve_solo_la_cuenta_con_saldo_en_cero()
    {
        await using var db = CreateContext();
        var repo = new AccountsPayableRepository(db);

        var (items, total) = await repo.GetPagedAsync(
            _tenantId,
            _companyId,
            AccountsPayableOriginType.PurchaseInvoice,
            AccountsPayableStatus.Paid,
            supplierId: null,
            page: 1,
            pageSize: 50
        );

        total.Should().Be(1);
        items.Should().ContainSingle(p => p.Id == _paidPayableId);
    }

    [Fact]
    public async Task Filtro_Pending_devuelve_solo_la_cuenta_con_saldo_pendiente()
    {
        await using var db = CreateContext();
        var repo = new AccountsPayableRepository(db);

        var (items, total) = await repo.GetPagedAsync(
            _tenantId,
            _companyId,
            AccountsPayableOriginType.PurchaseInvoice,
            AccountsPayableStatus.Pending,
            supplierId: null,
            page: 1,
            pageSize: 50
        );

        total.Should().Be(1);
        items.Should().ContainSingle(p => p.Id == _pendingPayableId);
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
        public bool HasCompanyContext => true;
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
}
