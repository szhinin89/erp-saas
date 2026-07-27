using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace ERP.Infrastructure.Tests.Persistence.Purchases;

/// <summary>
/// Suite de integración (PostgreSQL real vía Testcontainers) para
/// PurchaseReceptionDocumentRepository — Fase 2 de PurchaseReception. Cubre el mapeo EF, la
/// deduplicación por AccessKey (índice único real que InMemory no valida), el aislamiento
/// multi-tenant y la paginación.
/// </summary>
public sealed class PurchaseReceptionDocumentRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchasereception_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.Empty, Guid.Empty);
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);
        var branch = Branch.Create(
            tenant.Id, "Matriz", "Av. Principal 123", "001",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, true, _createdBy,
            companyId: company.Id);

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid tenantId, Guid companyId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(options, new FixedCurrentTenant(tenantId), new NoOpPublisher(), new FixedCurrentCompany(companyId));
    }

    private ErpDbContext CreateContext() => CreateContext(_tenantId, _companyId);

    private PurchaseReceptionDocument BuildDocument(string accessKey, Guid? tenantId = null, Guid? companyId = null, Guid? branchId = null)
        => PurchaseReceptionDocument.Create(
            tenantId ?? _tenantId, companyId ?? _companyId, branchId ?? _branchId,
            PurchaseReceptionSourceDocType.Invoice,
            "1791352688001", "QUALA ECUADOR S A", supplierId: null,
            accessKey, "015-027-000161740", new DateOnly(2026, 7, 1),
            new DateTime(2026, 7, 1, 21, 6, 55, DateTimeKind.Utc),
            15.96m, 2.4m, 18.35m, _createdBy);

    [Fact]
    public async Task AddAsync_persiste_el_documento_con_mapeo_correcto()
    {
        await using var db = CreateContext();
        var repo = new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(_companyId));
        var document = BuildDocument("0107202601179135268800120150270001617400016174011");

        await repo.AddAsync(document);
        await repo.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var verifyRepo = new PurchaseReceptionDocumentRepository(verifyDb, new FixedCurrentCompany(_companyId));
        var stored = await verifyRepo.GetByIdAsync(_tenantId, document.Id);

        stored.Should().NotBeNull();
        stored!.SupplierRuc.Should().Be("1791352688001");
        stored.AccessKey.Should().Be(document.AccessKey);
        stored.Status.Should().Be(PurchaseReceptionDocumentStatus.Imported);
        stored.TotalAmount.Should().Be(18.35m);
        stored.BranchId.Should().Be(_branchId);
        stored.CompanyId.Should().Be(_companyId);
    }

    [Fact]
    public async Task AttachSriAuthorization_persiste_XmlContent_AuthorizationNumber_y_XmlDownloadedAt()
    {
        await using var db = CreateContext();
        var repo = new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(_companyId));
        var document = BuildDocument("0607202601179135268800120150270001617400016174066");
        await repo.AddAsync(document);
        await repo.SaveChangesAsync();

        var downloadedAt = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);
        document.AttachSriAuthorization(
            document.AccessKey, new DateTime(2026, 7, 1, 21, 30, 0, DateTimeKind.Utc),
            "<factura>contenido autorizado</factura>", downloadedAt, [], _createdBy);
        await repo.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var verifyRepo = new PurchaseReceptionDocumentRepository(verifyDb, new FixedCurrentCompany(_companyId));
        var stored = await verifyRepo.GetByIdAsync(_tenantId, document.Id);

        stored.Should().NotBeNull();
        stored!.Status.Should().Be(PurchaseReceptionDocumentStatus.Verified);
        stored.XmlContent.Should().Be("<factura>contenido autorizado</factura>");
        stored.AuthorizationNumber.Should().Be(document.AccessKey);
        stored.XmlDownloadedAt.Should().Be(downloadedAt);
    }

    [Fact]
    public async Task ExistsByAccessKeyAsync_y_GetByAccessKeyAsync_reflejan_el_documento_creado()
    {
        await using var seedDb = CreateContext();
        var seedRepo = new PurchaseReceptionDocumentRepository(seedDb, new FixedCurrentCompany(_companyId));
        var document = BuildDocument("0207202601179135268800120150270001617400016174022");
        await seedRepo.AddAsync(document);
        await seedRepo.SaveChangesAsync();

        await using var queryDb = CreateContext();
        var queryRepo = new PurchaseReceptionDocumentRepository(queryDb, new FixedCurrentCompany(_companyId));

        (await queryRepo.ExistsByAccessKeyAsync(_tenantId, document.AccessKey)).Should().BeTrue();
        (await queryRepo.ExistsByAccessKeyAsync(_tenantId, "clave-inexistente")).Should().BeFalse();

        var found = await queryRepo.GetByAccessKeyAsync(_tenantId, document.AccessKey);
        found.Should().NotBeNull();
        found!.Id.Should().Be(document.Id);
    }

    [Fact]
    public async Task GetPagedAsync_pagina_y_ordena_por_fecha_de_emision_descendente()
    {
        await using var db = CreateContext();
        var repo = new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(_companyId));

        for (var i = 0; i < 3; i++)
            await repo.AddAsync(BuildDocument($"030720260117913526880012015027000161740001617{i:D4}"));
        await repo.SaveChangesAsync();

        await using var queryDb = CreateContext();
        var queryRepo = new PurchaseReceptionDocumentRepository(queryDb, new FixedCurrentCompany(_companyId));

        var (page1, total1) = await queryRepo.GetPagedAsync(_tenantId, page: 1, pageSize: 2);
        var (page2, total2) = await queryRepo.GetPagedAsync(_tenantId, page: 2, pageSize: 2);

        total1.Should().Be(3);
        total2.Should().Be(3);
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(1);
    }

    [Fact]
    public async Task No_permite_dos_documentos_con_la_misma_AccessKey_en_el_mismo_tenant()
    {
        var accessKey = "0407202601179135268800120150270001617400016174044";

        await using var db1 = CreateContext();
        var repo1 = new PurchaseReceptionDocumentRepository(db1, new FixedCurrentCompany(_companyId));
        await repo1.AddAsync(BuildDocument(accessKey));
        await repo1.SaveChangesAsync();

        await using var db2 = CreateContext();
        var repo2 = new PurchaseReceptionDocumentRepository(db2, new FixedCurrentCompany(_companyId));
        await repo2.AddAsync(BuildDocument(accessKey));

        var act = () => repo2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>()
            .Where(e => IsUniqueViolation(e.InnerException));
    }

    [Fact]
    public async Task Aislamiento_multi_tenant_un_tenant_no_ve_documentos_de_otro()
    {
        var otherTenant = Tenant.Create("Other Tenant", $"other-{Guid.NewGuid():N}"[..16], _createdBy);
        var otherCompany = Company.CreateManaged(otherTenant.Id, "1790099999001", "Other S.A.", createdBy: _createdBy);
        var otherBranch = Branch.Create(
            otherTenant.Id, "Matriz Otro", "Av. Secundaria 456", "001",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, true, _createdBy,
            companyId: otherCompany.Id);

        await using (var seedDb = CreateContext())
        {
            seedDb.Tenants.Add(otherTenant);
            seedDb.Companies.Add(otherCompany);
            seedDb.Branches.Add(otherBranch);
            await seedDb.SaveChangesAsync();
        }

        var accessKey = "0507202601179135268800120150270001617400016174055";

        await using (var db = CreateContext(otherTenant.Id, otherCompany.Id))
        {
            var repo = new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(otherCompany.Id));
            await repo.AddAsync(BuildDocument(accessKey, otherTenant.Id, otherCompany.Id, otherBranch.Id));
            await repo.SaveChangesAsync();
        }

        // El tenant original (distinto del que creó el documento) no debe verlo bajo ningún método.
        await using var queryDb = CreateContext();
        var queryRepo = new PurchaseReceptionDocumentRepository(queryDb, new FixedCurrentCompany(_companyId));

        (await queryRepo.ExistsByAccessKeyAsync(_tenantId, accessKey)).Should().BeFalse();
        var (items, total) = await queryRepo.GetPagedAsync(_tenantId, page: 1, pageSize: 50);
        items.Should().BeEmpty();
        total.Should().Be(0);

        // El tenant dueño sí lo ve.
        await using var ownerDb = CreateContext(otherTenant.Id, otherCompany.Id);
        var ownerRepo = new PurchaseReceptionDocumentRepository(ownerDb, new FixedCurrentCompany(otherCompany.Id));
        (await ownerRepo.ExistsByAccessKeyAsync(otherTenant.Id, accessKey)).Should().BeTrue();
    }

    private static bool IsUniqueViolation(Exception? inner)
        => inner is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;

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
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
