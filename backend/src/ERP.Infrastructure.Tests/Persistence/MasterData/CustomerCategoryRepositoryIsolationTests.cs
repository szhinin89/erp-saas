using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.MasterData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.MasterData;

/// <summary>
/// CLASS-BP-CATALOGS-01 — prueba de aislamiento tenant+company del repositorio genérico
/// <see cref="ClassificationCatalogRepositoryBase{TEntity}"/> usando <see cref="CustomerCategoryRepository"/>
/// como representante de los 12 catálogos (todos comparten la misma base). Cubre:
///   1. Fail-closed: sin TenantId/CompanyId válidos → 0 filas.
///   2. Aislamiento cross-company: la empresa B nunca ve filas de la empresa A.
///   3. CodeExistsActiveAsync respeta IsActive y el scope tenant+company.
/// </summary>
public sealed class CustomerCategoryRepositoryIsolationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_customercategory_isolation_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyA = Guid.NewGuid();
    private readonly Guid _companyB = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(_companyA);
        await db.Database.MigrateAsync();

        db.CustomerCategories.Add(
            CustomerCategory.CreateSystemSeeded(_tenantId, _companyA, "Retail", "Minorista", 1, _actor)
        );
        db.CustomerCategories.Add(
            CustomerCategory.CreateSystemSeeded(_tenantId, _companyB, "VIP", "VIP", 1, _actor)
        );
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid companyId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );
    }

    [Fact]
    public async Task GetActiveAsync_sin_tenant_ni_company_validos_devuelve_cero_filas()
    {
        await using var db = CreateContext(_companyA);
        var repo = new CustomerCategoryRepository(db);

        var result = await repo.GetActiveAsync(Guid.Empty, Guid.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAsync_empresa_A_no_ve_filas_de_empresa_B()
    {
        await using var db = CreateContext(_companyA);
        var repo = new CustomerCategoryRepository(db);

        var resultA = await repo.GetActiveAsync(_tenantId, _companyA);
        var resultB = await repo.GetActiveAsync(_tenantId, _companyB);

        resultA.Should().ContainSingle(e => e.Code == "Retail");
        resultA.Should().NotContain(e => e.Code == "VIP");
        resultB.Should().ContainSingle(e => e.Code == "VIP");
        resultB.Should().NotContain(e => e.Code == "Retail");
    }

    [Fact]
    public async Task CodeExistsActiveAsync_solo_matchea_dentro_del_scope_correcto()
    {
        await using var db = CreateContext(_companyA);
        var repo = new CustomerCategoryRepository(db);

        (await repo.CodeExistsActiveAsync(_tenantId, _companyA, "Retail")).Should().BeTrue();
        (await repo.CodeExistsActiveAsync(_tenantId, _companyB, "Retail")).Should().BeFalse();
        (await repo.CodeExistsActiveAsync(Guid.NewGuid(), _companyA, "Retail")).Should().BeFalse();
    }

    // ── Helpers de identidad para el DbContext ───────────────────────────────

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
