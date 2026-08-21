using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Purchases;

/// <summary>
/// ERP-CORE-CLOSEOUT-08 — PurchasePayableRepository.GetPagedAsync filtraba
/// <c>Status == "paid"</c> literalmente, pero <c>PurchasePayable.Status</c> nunca transiciona a
/// "paid" (RegisterPayment solo acumula PaidAmount) — el filtro "Pagadas" siempre devolvía cero
/// filas. Corregido para traducir "pending"/"paid" al saldo real (BalanceDue), mismo patrón que
/// SalesReceivableRepository. Usa Postgres real porque el bug es de traducción de la query EF, no
/// verificable con un mock.
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
    private readonly Guid _userId = Guid.NewGuid();
    private Guid _paidPayableId;
    private Guid _pendingPayableId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _tenantId = Guid.NewGuid();
        _companyId = Guid.NewGuid();

        var paid = PurchasePayable.Create(_tenantId, _companyId, Guid.NewGuid(), Guid.NewGuid(), 100m, _userId);
        paid.RegisterPayment(100m, _userId);
        _paidPayableId = paid.Id;

        var pending = PurchasePayable.Create(_tenantId, _companyId, Guid.NewGuid(), Guid.NewGuid(), 50m, _userId);
        _pendingPayableId = pending.Id;

        db.PurchasePayables.AddRange(paid, pending);
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
    public async Task Filtro_paid_devuelve_solo_la_cuenta_con_saldo_en_cero()
    {
        await using var db = CreateContext();
        var repo = new PurchasePayableRepository(db, new FixedCurrentCompany(_companyId));

        var (items, total) = await repo.GetPagedAsync(_tenantId, status: "paid", page: 1, pageSize: 50);

        total.Should().Be(1);
        items.Should().ContainSingle(p => p.Id == _paidPayableId);
    }

    [Fact]
    public async Task Filtro_pending_devuelve_solo_la_cuenta_con_saldo_pendiente()
    {
        await using var db = CreateContext();
        var repo = new PurchasePayableRepository(db, new FixedCurrentCompany(_companyId));

        var (items, total) = await repo.GetPagedAsync(_tenantId, status: "pending", page: 1, pageSize: 50);

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
