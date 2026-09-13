using ERP.Application.Common;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Configuration;

/// <summary>
/// COMPANY-PRECISION-POLICY-SSOT-01 (PostgreSQL 16 real vía Testcontainers). Cubre: (1) el
/// backfill de la migración AddCompanyPrecisionPolicy crea una fila por empresa existente con el
/// perfil Estándar comercial por defecto; (2) si existían valores previos en org_settings
/// (namespace Presentation), se mapean y clampean a los nuevos rangos; (3) unique(tenant_id,
/// company_id); (4) el filtro por tenant_id/company_id no fuga entre empresas. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CompanyPrecisionPolicyMigrationTests : IAsyncLifetime
{
    private const string MigrationBeforeCompanyPrecisionPolicy =
        "20260913131221_AddSalesInvoiceDetailHistoricalPricingFields";

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
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );

    [Fact]
    public async Task Backfill_crea_fila_StandardCommercial_para_empresa_sin_org_settings_previos()
    {
        Guid tenantId;
        Guid companyId;

        await using (var db = CreateContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(MigrationBeforeCompanyPrecisionPolicy);

            var createdBy = Guid.NewGuid();
            var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], createdBy);
            var company = Company.CreateManaged(
                tenant.Id,
                "1790012345001",
                "Test S.A.",
                createdBy: createdBy
            );
            db.Tenants.Add(tenant);
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;
            companyId = company.Id;
        }

        await using (var db = CreateContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync();
        }

        await using (var db = CreateContext(tenantId, companyId))
        {
            var policy = await db.CompanyPrecisionPolicies.SingleAsync(p =>
                p.TenantId == tenantId && p.CompanyId == companyId
            );

            policy.ProfileType.Should().Be(PrecisionProfileType.StandardCommercial);
            policy.SalesUnitPriceDecimals.Should().Be(2);
            policy.PurchaseUnitPriceDecimals.Should().Be(4);
            policy.QuantityDecimals.Should().Be(4);
            policy.PercentageDecimals.Should().Be(2);
            policy.UnitCostDecimals.Should().Be(6);
            policy.AverageCostDecimals.Should().Be(6);
            policy.ConversionFactorDecimals.Should().Be(6);
            policy.SettlementToleranceAmount.Should().Be(0.01m);
            policy.IsLocked.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Backfill_mapea_y_clampea_valores_previos_de_org_settings_Presentation()
    {
        Guid tenantId;
        Guid companyId;

        await using (var db = CreateContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync(MigrationBeforeCompanyPrecisionPolicy);

            var createdBy = Guid.NewGuid();
            var tenant = Tenant.Create("Test Tenant 2", $"test-{Guid.NewGuid():N}"[..16], createdBy);
            var company = Company.CreateManaged(
                tenant.Id,
                "1790012345002",
                "Test S.A. 2",
                createdBy: createdBy
            );
            db.Tenants.Add(tenant);
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;
            companyId = company.Id;

            // sales_unit_price = 6 (dentro del rango nuevo 2-8, no se clampea).
            // purchase_unit_price = 0 (fuera del rango nuevo 2-8 → clampea a 2).
            // quantity = 4 (dentro de 0-6, no se clampea).
            // percentage = 6 (límite superior exacto del rango nuevo 2-6, no requiere clamp).
            db.OrgSettings.Add(
                OrgSetting.Create(
                    tenantId,
                    companyId,
                    OrgScope.Company,
                    companyId,
                    OrgSettingKeys.Presentation.DecimalSalesUnitPrice,
                    "6",
                    SettingDataType.Int,
                    createdBy
                )
            );
            db.OrgSettings.Add(
                OrgSetting.Create(
                    tenantId,
                    companyId,
                    OrgScope.Company,
                    companyId,
                    OrgSettingKeys.Presentation.DecimalPurchaseUnitPrice,
                    "0",
                    SettingDataType.Int,
                    createdBy
                )
            );
            db.OrgSettings.Add(
                OrgSetting.Create(
                    tenantId,
                    companyId,
                    OrgScope.Company,
                    companyId,
                    OrgSettingKeys.Presentation.DecimalQuantity,
                    "4",
                    SettingDataType.Int,
                    createdBy
                )
            );
            db.OrgSettings.Add(
                OrgSetting.Create(
                    tenantId,
                    companyId,
                    OrgScope.Company,
                    companyId,
                    OrgSettingKeys.Presentation.DecimalPercentage,
                    "6",
                    SettingDataType.Int,
                    createdBy
                )
            );
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            await db.Database.GetService<IMigrator>().MigrateAsync();
        }

        await using (var db = CreateContext(tenantId, companyId))
        {
            var policy = await db.CompanyPrecisionPolicies.SingleAsync(p =>
                p.TenantId == tenantId && p.CompanyId == companyId
            );

            policy.SalesUnitPriceDecimals.Should().Be(6);
            policy.PurchaseUnitPriceDecimals.Should().Be(2, because: "0 está fuera de [2,8] y se clampea al mínimo");
            policy.QuantityDecimals.Should().Be(4);
            policy.PercentageDecimals.Should().Be(6);
            // unit_cost/average_cost/conversion_factor/tolerance nunca existieron en Presentation:
            // siempre toman el default de Estándar comercial.
            policy.UnitCostDecimals.Should().Be(6);
            policy.AverageCostDecimals.Should().Be(6);
            policy.ConversionFactorDecimals.Should().Be(6);
            policy.SettlementToleranceAmount.Should().Be(0.01m);
        }
    }

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
        var companyA = Company.CreateManaged(tenant.Id, "1790012345004", "A S.A.", createdBy: createdBy);
        var companyB = Company.CreateManaged(tenant.Id, "1790012345005", "B S.A.", createdBy: createdBy);
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
