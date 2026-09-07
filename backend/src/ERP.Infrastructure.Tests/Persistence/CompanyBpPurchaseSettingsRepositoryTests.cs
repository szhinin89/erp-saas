using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// ADR-033 — CompanyBpPurchaseSettings (default de condición de pago de proveedor por empresa).
///
/// El backfill original desde SupplierRoleConfig.PaymentTermId (Fase 3a, migración
/// AddCompanyBpPurchaseSettings) fue una operación histórica de una sola ejecución; sus pruebas se
/// retiraron al eliminar ese campo del dominio (SupplierRoleConfig ya no tiene condición de pago —
/// CompanyBpPurchaseSettings.PaymentTermId es la única fuente). El resto de la suite verifica el
/// comportamiento vigente del repositorio.
/// </summary>
public sealed class CompanyBpPurchaseSettingsRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_cbps_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid tenantId, Guid? ambientCompanyId = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        var company = ambientCompanyId is { } cid
            ? new FixedCurrentCompany(cid, hasCompanyContext: true)
            : new FixedCurrentCompany(Guid.Empty, hasCompanyContext: false);

        return new ErpDbContext(options, new FixedCurrentTenant(tenantId), new NoOpPublisher(), company);
    }

    [Fact]
    public async Task Repository_aisla_por_empresa_no_devuelve_default_de_otra_empresa_del_mismo_proveedor()
    {
        var createdBy = Guid.NewGuid();
        await using var migrate = CreateContext(Guid.NewGuid());
        await migrate.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], createdBy);
        var companyA = Company.CreateManaged(tenant.Id, "1790012345005", "Empresa A", createdBy: createdBy);
        var companyB = Company.CreateManaged(tenant.Id, "1790012345006", "Empresa B", createdBy: createdBy);
        var supplier = BusinessPartner.Create(tenant.Id, "04", "1791352688005", 2, "Proveedor Compartido", createdBy);

        await using var seed = CreateContext(tenant.Id);
        seed.Tenants.Add(tenant);
        seed.Companies.Add(companyA);
        seed.Companies.Add(companyB);
        seed.BusinessPartners.Add(supplier);
        await seed.SaveChangesAsync();

        var paymentTermA = Guid.NewGuid();
        var paymentTermB = Guid.NewGuid();

        await using (var writeA = CreateContext(tenant.Id, companyA.Id))
        {
            var repo = new CompanyBpPurchaseSettingsRepository(writeA);
            await repo.AddAsync(
                CompanyBpPurchaseSettings.Create(tenant.Id, companyA.Id, supplier.Id, paymentTermA, createdBy)
            );
            await repo.SaveChangesAsync();
        }
        await using (var writeB = CreateContext(tenant.Id, companyB.Id))
        {
            var repo = new CompanyBpPurchaseSettingsRepository(writeB);
            await repo.AddAsync(
                CompanyBpPurchaseSettings.Create(tenant.Id, companyB.Id, supplier.Id, paymentTermB, createdBy)
            );
            await repo.SaveChangesAsync();
        }

        await using var readAsA = CreateContext(tenant.Id, companyA.Id);
        var resultForA = await new CompanyBpPurchaseSettingsRepository(readAsA)
            .GetByBusinessPartnerAsync(supplier.Id);

        resultForA.Should().NotBeNull();
        resultForA!.CompanyId.Should().Be(companyA.Id);
        resultForA.PaymentTermId.Should().Be(paymentTermA);
        resultForA.PaymentTermId.Should().NotBe(paymentTermB);
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId, bool hasCompanyContext) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => hasCompanyContext;
        public bool HasCompanyContext => hasCompanyContext;
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
