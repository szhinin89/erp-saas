using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// FASE 5 PURCHASE-UX — migración <c>PurchaseLineXmlOriginPresentation</c> contra PostgreSQL real:
/// la presentación de origen XML (nivel + factor snapshot) se persiste separada de la operativa y
/// sobrevive guardar → reabrir → guardar; las líneas anteriores (sin origen) quedan en null.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseLineXmlOriginPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_xml_origin_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private static readonly Guid SixPackLevelId = Guid.NewGuid();
    private static readonly Guid UnitLevelId = Guid.NewGuid();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _paymentTermId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);
        var branch = Branch.Create(
            tenant.Id,
            "Matriz",
            "Av. Principal 123",
            "001",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            _createdBy,
            companyId: company.Id
        );
        var supplier = BusinessPartner.Create(
            tenant.Id,
            TaxIdentification.SriRuc,
            "1791352688001",
            2,
            "Proveedor Test",
            _createdBy
        );
        var paymentTerm = PaymentTerm.Create(
            tenant.Id,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(supplier);
        db.PaymentTerms.Add(paymentTerm);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _supplierId = supplier.Id;
        _paymentTermId = paymentTerm.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private PurchaseInvoiceDetail XmlLineInBaseUnit(Guid invoiceId, (Guid LevelId, decimal Factor)? origin)
    {
        // XML editada 24 → 23: operativa en unidad base (nivel base, factor 1), origen SIXPACK x6.
        var line = PurchaseInvoiceDetail.Create(
            invoiceId,
            _tenantId,
            "FANTA",
            23m,
            2m,
            "10",
            "UNIT",
            purchaseReceptionLineId: Guid.NewGuid(),
            packagingLevelId: UnitLevelId,
            conversionFactor: 1m,
            baseUomCode: "UNIT"
        );
        line.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);
        if (origin is { } o)
            line.SetXmlOrigin(o.LevelId, o.Factor);
        return line;
    }

    private async Task<Guid> SeedDraftAsync((Guid LevelId, decimal Factor)? origin)
    {
        await using var db = CreateContext();
        var inv = PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor Test",
            "1791352688001",
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999):D9}",
            new DateOnly(2026, 10, 12),
            _createdBy,
            _paymentTermId,
            "Contado",
            1,
            0
        );
        inv.ReplaceLines([XmlLineInBaseUnit(inv.Id, origin)], _createdBy);
        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();
        return inv.Id;
    }

    private async Task<PurchaseInvoice> ReopenAsync(Guid invoiceId)
    {
        await using var db = CreateContext();
        var repo = new PurchaseInvoiceRepository(db, new FixedCurrentCompany(_companyId));
        return (await repo.GetByIdAsync(_tenantId, invoiceId))!;
    }

    [Fact]
    public async Task Origen_x6_sobrevive_guardar_reabrir_separado_de_la_presentacion_operativa()
    {
        var invoiceId = await SeedDraftAsync((SixPackLevelId, 6m));

        var line = (await ReopenAsync(invoiceId)).Lines.Single();

        line.PackagingLevelId.Should().Be(UnitLevelId);
        line.ConversionFactor.Should().Be(1m);
        line.QuantityInBaseUom.Should().Be(23m);
        line.XmlOriginPackagingLevelId.Should().Be(SixPackLevelId);
        line.XmlOriginFactor.Should().Be(6m);
    }

    [Fact]
    public async Task Guardar_de_nuevo_conserva_el_origen_y_la_cantidad_a_registrar()
    {
        var invoiceId = await SeedDraftAsync((SixPackLevelId, 6m));

        for (var round = 0; round < 3; round++)
        {
            await using var db = CreateContext();
            var repo = new PurchaseInvoiceRepository(db, new FixedCurrentCompany(_companyId));
            var inv = (await repo.GetByIdAsync(_tenantId, invoiceId))!;
            var previous = inv.Lines.Single();
            // Misma secuencia que UpdatePurchaseDraftHandler: DELETE directo de las líneas
            // anteriores y reconstrucción del agregado con las nuevas.
            var newLines = new[]
            {
                XmlLineInBaseUnit(
                    inv.Id,
                    (previous.XmlOriginPackagingLevelId!.Value, previous.XmlOriginFactor!.Value)
                ),
            };
            await repo.RemoveLinesByInvoiceAsync(inv.Id, newLines);
            inv.ReplaceLines(newLines, _createdBy);
            await db.SaveChangesAsync();
        }

        var line = (await ReopenAsync(invoiceId)).Lines.Single();
        line.QuantityInBaseUom.Should().Be(23m);
        line.XmlOriginPackagingLevelId.Should().Be(SixPackLevelId);
        line.XmlOriginFactor.Should().Be(6m);
    }

    [Fact]
    public async Task Linea_sin_origen_persistido_queda_en_null_compatible_con_borradores_anteriores()
    {
        var invoiceId = await SeedDraftAsync(origin: null);

        var line = (await ReopenAsync(invoiceId)).Lines.Single();

        line.XmlOriginPackagingLevelId.Should().BeNull();
        line.XmlOriginFactor.Should().BeNull();
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
