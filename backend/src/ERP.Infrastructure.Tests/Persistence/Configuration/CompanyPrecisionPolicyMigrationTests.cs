using ERP.Application.Common;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Configuration;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01 (PostgreSQL 16 real vía Testcontainers). Cubre sobre la cadena
/// de migraciones vigente: (1) unique(tenant_id, company_id); (2) el filtro por
/// tenant_id/company_id no fuga entre empresas. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CompanyPrecisionPolicyMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_precision_policy_migration_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid tenantId = default, Guid companyId = default) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(_postgres.GetConnectionString())
                .Options,
            new FixedCurrentTenant(tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );

    [Fact]
    public async Task Unique_tenant_company_se_respeta()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant 3", $"test-{Guid.NewGuid():N}"[..16], createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345003",
            "Test S.A. 3",
            createdBy: createdBy
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        db.CompanyPrecisionPolicies.Add(
            CompanyPrecisionPolicy.CreateStandardCommercial(tenant.Id, company.Id, createdBy)
        );
        await db.SaveChangesAsync();

        db.CompanyPrecisionPolicies.Add(
            CompanyPrecisionPolicy.CreateStandardCommercial(tenant.Id, company.Id, createdBy)
        );
        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Query_filtrada_por_tenant_y_company_no_fuga_entre_empresas()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant 4", $"test-{Guid.NewGuid():N}"[..16], createdBy);
        var companyA = Company.CreateManaged(
            tenant.Id,
            "1790012345004",
            "A S.A.",
            createdBy: createdBy
        );
        var companyB = Company.CreateManaged(
            tenant.Id,
            "1790012345005",
            "B S.A.",
            createdBy: createdBy
        );
        db.Tenants.Add(tenant);
        db.Companies.AddRange(companyA, companyB);
        await db.SaveChangesAsync();

        db.CompanyPrecisionPolicies.Add(
            CompanyPrecisionPolicy.CreateStandardCommercial(tenant.Id, companyA.Id, createdBy)
        );
        db.CompanyPrecisionPolicies.Add(
            CompanyPrecisionPolicy.CreateHighPrecision(tenant.Id, companyB.Id, createdBy)
        );
        await db.SaveChangesAsync();

        // ErpDbContext aplica un query filter GLOBAL por (TenantId, CompanyId) del contexto
        // actual (EnterpriseQueryFilterConfigurator, vía ITenantScopedEntity/ICompanyScopedEntity)
        // — un contexto abierto con la empresa A activa nunca puede ver filas de la empresa B, sin
        // importar qué predicado explícito se agregue. Esto es la prueba real de fail-closed:
        // el aislamiento no depende de que cada query recuerde filtrar manualmente.
        await using var verifyDbAsCompanyA = CreateContext(tenant.Id, companyA.Id);
        var visibleToCompanyA = await verifyDbAsCompanyA.CompanyPrecisionPolicies.ToListAsync();
        visibleToCompanyA.Should().ContainSingle();
        visibleToCompanyA[0].CompanyId.Should().Be(companyA.Id);
        visibleToCompanyA[0].ProfileType.Should().Be(PrecisionProfileType.StandardCommercial);

        await using var verifyDbAsCompanyB = CreateContext(tenant.Id, companyB.Id);
        var visibleToCompanyB = await verifyDbAsCompanyB.CompanyPrecisionPolicies.ToListAsync();
        visibleToCompanyB.Should().ContainSingle();
        visibleToCompanyB[0].CompanyId.Should().Be(companyB.Id);
        visibleToCompanyB[0].ProfileType.Should().Be(PrecisionProfileType.HighPrecision);
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
