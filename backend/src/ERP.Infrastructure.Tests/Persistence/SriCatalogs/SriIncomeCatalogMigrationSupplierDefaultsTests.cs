using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.SriCatalogs;

/// <summary>
/// ZH-SRI-RETENTION-INCOME-CATALOG-SSOT-01 — decisión del propietario: un default de proveedor que apunta a un
/// concepto de Renta cuyo significado cambió (o que fue retirado del Catálogo ATS vigente) no sigue aplicándose
/// en silencio. Se ejercita la migración REAL: base migrada hasta la migración previa, defaults existentes, y
/// luego <c>SriRetentionIncomeCatalogAts20260806</c>.
/// </summary>
public sealed class SriIncomeCatalogMigrationSupplierDefaultsTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261002170222_SriRetentionCatalogVersioning";

    private static readonly Guid Concept303 = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid Concept304 = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid Concept312 = Guid.Parse("20000000-0000-0000-0000-000000000006");
    private static readonly Guid Concept327 = Guid.Parse("20000000-0000-0000-0000-000000000009");
    private static readonly Guid Concept341 = Guid.Parse("20000000-0000-0000-0000-000000000010");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_sri_income_migration_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _userId = Guid.NewGuid();
    private Guid _tenantId;
    private Guid _companyId;

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task La_migracion_deshabilita_solo_los_defaults_cuyo_concepto_cambio_de_significado_o_fue_retirado()
    {
        await using (var db = CreateContext())
            await db.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        var defaults = await SeedSupplierDefaultsAsync(Concept303, Concept304, Concept312, Concept327, Concept341);

        await using (var db = CreateContext())
            await db.Database.MigrateAsync();

        await using var check = CreateContext();
        var after = await check.SupplierRetentionDefaults.AsNoTracking().ToDictionaryAsync(d => d.SriRetentionCodeId);

        after.Should().HaveCount(5, "ningún default se elimina");
        after[Concept303].IsActive.Should().BeTrue("303 conserva su significado");
        after[Concept312].IsActive.Should().BeTrue("312 conserva su significado (solo cambia la tasa)");
        after[Concept304].IsActive.Should().BeFalse("304 cambió de significado: requiere validación explícita");
        after[Concept327].IsActive.Should().BeFalse("327 cambió de significado y su tasa es condicional");
        after[Concept341].IsActive.Should().BeFalse("341 ya no existe en el Catálogo ATS vigente");

        foreach (var (codeId, id) in defaults)
        {
            after[codeId].Id.Should().Be(id, "no se remapea a otro código ni se recrea");
            after[codeId].SriRetentionCodeId.Should().Be(codeId);
        }
        after[Concept304].UpdatedBy.Should().Be(Guid.Empty, "actor de sistema");
    }

    private async Task<Dictionary<Guid, Guid>> SeedSupplierDefaultsAsync(params Guid[] conceptIds)
    {
        await using (var bootstrap = CreateContext())
        {
            var tenant = Tenant.Create("RETMIG", $"retmig-{Guid.NewGuid():N}"[..16], _userId);
            var company = Company.CreateManaged(tenant.Id, "1790012345001", "Retenedora S.A.", createdBy: _userId);
            bootstrap.Tenants.Add(tenant);
            bootstrap.Companies.Add(company);
            await bootstrap.SaveChangesAsync();
            _tenantId = tenant.Id;
            _companyId = company.Id;
        }

        await using var db = CreateContext();
        var supplier = BusinessPartner.Create(_tenantId, TaxIdentification.SriRuc, "1791352688001", 2, "Proveedor", _userId);
        db.BusinessPartners.Add(supplier);
        var result = new Dictionary<Guid, Guid>();
        var order = 0;
        foreach (var conceptId in conceptIds)
        {
            var entry = SupplierRetentionDefault.Create(_tenantId, _companyId, supplier.Id, conceptId, order++, _userId);
            db.SupplierRetentionDefaults.Add(entry);
            result[conceptId] = entry.Id;
        }
        await db.SaveChangesAsync();
        return result;
    }

    private ErpDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedTenant(() => _tenantId),
            new NoOpPublisher(),
            new FixedCompany(() => _companyId)
        );

    private sealed class FixedTenant(Func<Guid> tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId();
        public string? Slug => null;
    }

    private sealed class FixedCompany(Func<Guid> companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId();
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
