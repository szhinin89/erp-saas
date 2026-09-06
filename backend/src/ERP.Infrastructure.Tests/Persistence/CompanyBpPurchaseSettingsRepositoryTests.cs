using ERP.Application.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.MasterData.ValueObjects;
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
/// ADR-033, Fase 3a — CompanyBpPurchaseSettings (default de condición de pago de proveedor por
/// empresa) y su backfill desde SupplierRoleConfig.PaymentTermId (migración
/// AddCompanyBpPurchaseSettings).
///
/// El backfill se ejecuta ANTES de que exista cualquier dato de negocio (MigrateAsync corre sobre
/// una BD vacía), así que para probar su lógica real se re-ejecuta el mismo SQL de la migración
/// contra datos ya sembrados — esto evita usar la API interna de IMigrator para aplicar
/// migraciones parciales (frágil entre versiones de EF) y prueba exactamente el comportamiento
/// que importa: qué filas produce esa consulta. Si el SQL de la migración cambia, este test debe
/// actualizarse en paralelo.
/// </summary>
public sealed class CompanyBpPurchaseSettingsRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_cbps_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private const string BackfillSql =
        """
        INSERT INTO master_company_bp_purchase_settings
            (id, tenant_id, company_id, business_partner_id, payment_term_id, created_at, created_by)
        SELECT
            gen_random_uuid(), r.tenant_id, c.id, r.business_partner_id, sc.payment_term_id, NOW(), r.created_by
        FROM master_bp_roles r
        JOIN master_bp_supplier_configs sc ON sc.role_id = r.id
        JOIN master_business_partners bp ON bp.id = r.business_partner_id
        JOIN company c ON c.tenant_id = r.tenant_id
        WHERE r.role_type = 2
          AND r.is_active = true
          AND bp.is_active = true
          AND c.is_active = true
          AND NOT EXISTS (
              SELECT 1 FROM master_company_bp_purchase_settings x
              WHERE x.tenant_id = r.tenant_id
                AND x.company_id = c.id
                AND x.business_partner_id = r.business_partner_id
          );
        """;

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
    public async Task Backfill_crea_fila_solo_para_empresa_activa_y_proveedor_activo_con_semilla_correcta()
    {
        var createdBy = Guid.NewGuid();
        await using var migrate = CreateContext(Guid.NewGuid());
        await migrate.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], createdBy);
        var companyActive = Company.CreateManaged(tenant.Id, "1790012345001", "Empresa Activa", createdBy: createdBy);
        var companyInactive = Company.CreateManaged(tenant.Id, "1790012345002", "Empresa Inactiva", createdBy: createdBy);

        var seedPaymentTerm = PaymentTerm.Create(tenant.Id, "30D", "Crédito 30 días", 1, 30, createdBy);

        var activeSupplier = BusinessPartner.Create(tenant.Id, "04", "1791352688001", 2, "Proveedor Activo", createdBy);
        var activeSupplierRole = BusinessPartnerRole.Create(
            tenant.Id, activeSupplier.Id, RoleType.Supplier, createdBy,
            SupplierRoleConfig.Create(seedPaymentTerm.Id)
        );

        // Proveedor con rol revocado — no debe generar fila aunque la empresa esté activa.
        var revokedSupplier = BusinessPartner.Create(tenant.Id, "04", "1791352688002", 2, "Proveedor Rol Revocado", createdBy);
        var revokedSupplierRole = BusinessPartnerRole.Create(
            tenant.Id, revokedSupplier.Id, RoleType.Supplier, createdBy,
            SupplierRoleConfig.Create(seedPaymentTerm.Id)
        );
        revokedSupplierRole.Revoke(createdBy);

        // BusinessPartner desactivado — no debe generar fila aunque el rol siga activo.
        var inactiveBpSupplier = BusinessPartner.Create(tenant.Id, "04", "1791352688003", 2, "Proveedor BP Inactivo", createdBy);
        var inactiveBpSupplierRole = BusinessPartnerRole.Create(
            tenant.Id, inactiveBpSupplier.Id, RoleType.Supplier, createdBy,
            SupplierRoleConfig.Create(seedPaymentTerm.Id)
        );
        inactiveBpSupplier.Deactivate(createdBy);

        await using var seed = CreateContext(tenant.Id);
        seed.Tenants.Add(tenant);
        seed.Companies.Add(companyActive);
        seed.Companies.Add(companyInactive);
        seed.PaymentTerms.Add(seedPaymentTerm);
        seed.BusinessPartners.Add(activeSupplier);
        seed.BusinessPartners.Add(revokedSupplier);
        seed.BusinessPartners.Add(inactiveBpSupplier);
        seed.BusinessPartnerRoles.Add(activeSupplierRole);
        seed.BusinessPartnerRoles.Add(revokedSupplierRole);
        seed.BusinessPartnerRoles.Add(inactiveBpSupplierRole);
        await seed.SaveChangesAsync();

        // Company.IsActive es propiedad exclusiva de Companies Admin/Platform — sin método de
        // dominio en ERP Core para desactivarla; se fuerza vía SQL directo solo para este fixture.
        await seed.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE company SET is_active = false WHERE id = {companyInactive.Id}"
        );

        // Ejecuta el mismo backfill de la migración contra los datos ya sembrados.
        await seed.Database.ExecuteSqlRawAsync(BackfillSql);

        // IgnoreQueryFilters: verificación cruza intencionalmente ambas empresas (el contexto de
        // seed no tiene company ambiente) — no es un query de negocio, solo de aserción de test.
        var rows = await seed.CompanyBpPurchaseSettings.IgnoreQueryFilters().AsNoTracking().ToListAsync();

        rows.Should().ContainSingle(r =>
            r.CompanyId == companyActive.Id
            && r.BusinessPartnerId == activeSupplier.Id
            && r.PaymentTermId == seedPaymentTerm.Id
        );
        rows.Should().NotContain(r => r.CompanyId == companyInactive.Id);
        rows.Should().NotContain(r => r.BusinessPartnerId == revokedSupplier.Id);
        rows.Should().NotContain(r => r.BusinessPartnerId == inactiveBpSupplier.Id);
    }

    [Fact]
    public async Task Backfill_es_idempotente_no_duplica_al_reejecutarse()
    {
        var createdBy = Guid.NewGuid();
        await using var migrate = CreateContext(Guid.NewGuid());
        await migrate.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345003", "Empresa", createdBy: createdBy);
        var seedPaymentTerm = PaymentTerm.Create(tenant.Id, "CONT", "Contado", 1, 0, createdBy);
        var supplier = BusinessPartner.Create(tenant.Id, "04", "1791352688004", 2, "Proveedor", createdBy);
        var role = BusinessPartnerRole.Create(
            tenant.Id, supplier.Id, RoleType.Supplier, createdBy,
            SupplierRoleConfig.Create(seedPaymentTerm.Id)
        );

        await using var seed = CreateContext(tenant.Id);
        seed.Tenants.Add(tenant);
        seed.Companies.Add(company);
        seed.PaymentTerms.Add(seedPaymentTerm);
        seed.BusinessPartners.Add(supplier);
        seed.BusinessPartnerRoles.Add(role);
        await seed.SaveChangesAsync();

        await seed.Database.ExecuteSqlRawAsync(BackfillSql);
        var afterFirstRun = await seed.CompanyBpPurchaseSettings.IgnoreQueryFilters().AsNoTracking().CountAsync();

        // Re-ejecutar el mismo backfill no debe duplicar ni lanzar violación de índice único.
        var act = async () => await seed.Database.ExecuteSqlRawAsync(BackfillSql);
        await act.Should().NotThrowAsync();

        var afterSecondRun = await seed.CompanyBpPurchaseSettings.IgnoreQueryFilters().AsNoTracking().CountAsync();
        afterSecondRun.Should().Be(afterFirstRun);
        afterFirstRun.Should().Be(1);
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
